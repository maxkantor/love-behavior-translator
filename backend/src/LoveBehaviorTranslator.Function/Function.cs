using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LoveBehaviorTranslator.Function;

public sealed class Function
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAmazonDynamoDB _ddb;
    private readonly IAmazonSecretsManager _secrets;
    private readonly IAmazonS3 _s3;
    private readonly IAmazonSimpleEmailService _ses;
    private readonly HttpClient _http;

    private readonly string _tableName;
    private readonly string _bucketName;
    private readonly string _openAiSecretArn;
    private readonly string _openAiModel;
    private readonly int _rateLimitPerMinute;
    private readonly int _rateLimitBurst;
    private readonly string _sesFromEmail;

    public Function()
    {
        _ddb = new AmazonDynamoDBClient();
        _secrets = new AmazonSecretsManagerClient();
        _s3 = new AmazonS3Client();
        _ses = new AmazonSimpleEmailServiceClient();
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        _tableName = GetEnv("TABLE_NAME");
        _bucketName = GetEnv("ARTIFACTS_BUCKET");
        _openAiSecretArn = GetEnv("OPENAI_SECRET_ARN");
        _openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1-mini";
        _rateLimitPerMinute = int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_PER_MINUTE"), out var rpm) ? rpm : 10;
        _rateLimitBurst = int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_BURST"), out var burst) ? burst : 5;
        _sesFromEmail = Environment.GetEnvironmentVariable("SES_FROM_EMAIL") ?? "";
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Get path - handle proxy resources by using request path directly
            var rawPath = request.Path ?? "";
            // For proxy resources, path might include the full path
            var path = rawPath.TrimEnd('/').ToLowerInvariant();
            var method = (request.HttpMethod ?? "GET").ToUpperInvariant();
            
            // Log for debugging (remove in production)
            context.Logger.LogInformation($"Request: {method} {path}");

            // Handle CORS preflight (OPTIONS) requests
            if (method == "OPTIONS")
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        ["Access-Control-Allow-Origin"] = "*",
                        ["Access-Control-Allow-Headers"] = "Content-Type,Authorization,x-user-id",
                        ["Access-Control-Allow-Methods"] = "OPTIONS,GET,POST,PUT",
                        ["Access-Control-Max-Age"] = "3600"
                    },
                    Body = ""
                };
            }

            // Health check
            if (method == "GET" && (path.EndsWith("/health") || path == "/health"))
                return JsonResponse(200, new { status = "healthy" });

            // Admin login (no auth required)
            if (method == "POST" && (path.EndsWith("/admin/login") || path == "/admin/login"))
                return await HandleAdminLogin(request, context);

            // Admin routes (require authentication)
            if (path.StartsWith("/admin"))
            {
                if (!AdminSystem.VerifyAdminToken(request, _secrets))
                    return JsonResponse(401, new { error = "Unauthorized" });

                // Handle admin routes - check exact matches first
                if (method == "GET" && path == "/admin/users")
                    return await HandleAdminGetUsers(context);

                if (method == "GET" && path == "/admin/dashboard")
                    return await HandleAdminDashboard(context);

                if (method == "PUT" && path.Contains("/admin/users/") && path.EndsWith("/credits"))
                    return await HandleAdminSetUserCredits(request, context);

                if (method == "POST" && path.Contains("/admin/users/") && path.EndsWith("/credits"))
                    return await HandleAdminGrantUserCredits(request, context);

                if (method == "PUT" && (path.EndsWith("/admin/me/credits") || path == "/admin/me/credits"))
                    return await HandleAdminSetMyCredits(request, context);

                if (method == "GET" && (path.EndsWith("/admin/contacts") || path == "/admin/contacts"))
                    return await HandleAdminGetContacts(context);

                if (method == "POST" && path.Contains("/admin/contacts/") && path.EndsWith("/reply"))
                    return await HandleAdminReplyContact(request, context);

                // If we're in /admin but no route matched, return 404
                return JsonResponse(404, new { error = "Admin endpoint not found" });
            }

            // Get user credits endpoint
            if (method == "GET" && (path.EndsWith("/credits") || path == "/credits"))
                return await HandleGetCredits(request, context);

            // Contact form endpoint
            if (method == "POST" && (path.EndsWith("/contact") || path == "/contact"))
                return await HandleContact(request, context);

            // Analyze endpoint
            if (method == "POST" && (path.EndsWith("/analyze") || path == "/analyze"))
                return await HandleAnalyze(request, context);

            return JsonResponse(404, new { error = "Not found" });
        }
        catch (ClientVisibleException ex)
        {
            return JsonResponse(ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Unhandled error: {ex}");
            return JsonResponse(500, new { error = "Internal server error" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleGetCredits(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var ip = GetClientIp(request) ?? "unknown";
        var userId = CreditSystem.GetUserId(request, ip);
        
        try
        {
            var credits = await CreditSystem.GetUserCredits(userId, _ddb, context.Logger);
            return JsonResponse(200, new { credits, userId });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting credits: {ex}");
            return JsonResponse(500, new { error = "Failed to get credits" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleAnalyze(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var ip = GetClientIp(request) ?? "unknown";
        var userId = CreditSystem.GetUserId(request, ip);

        // Check credits before processing
        var hasCredits = await CreditSystem.HasCredits(userId, _ddb, context.Logger);
        if (!hasCredits)
        {
            var currentCredits = await CreditSystem.GetUserCredits(userId, _ddb, context.Logger);
            return JsonResponse(402, new { 
                error = "Insufficient credits", 
                detail = $"You have {currentCredits} credits remaining. Please purchase more credits to continue.",
                credits = currentCredits
            });
        }

        // Rate limit (DynamoDB-backed)
        var allowed = await RateLimitCheck(ip, context);
        if (!allowed)
            return JsonResponse(429, new { error = "Rate limit exceeded", detail = "Please wait a bit and try again." });

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ClientVisibleException(400, "Request body is required.");

        BehaviorAnalysisRequest? input;
        try
        {
            input = JsonSerializer.Deserialize<BehaviorAnalysisRequest>(request.Body, Json);
        }
        catch
        {
            throw new ClientVisibleException(400, "Invalid JSON body.");
        }

        if (input is null)
            throw new ClientVisibleException(400, "Invalid request.");

        input = input.NormalizeAndValidate();

        var prompt = PromptFactory.BuildPrompt(input);
        var openAiKey = await GetOpenAiApiKey();

        var raw = await CallOpenAi(prompt, openAiKey, context);
        var formatted = PromptFactory.FormatResponse(raw, input.AnalysisMode);

        // Deduct credit after successful analysis
        await CreditSystem.DeductCredit(userId, _ddb, context.Logger);
        var remainingCredits = await CreditSystem.GetUserCredits(userId, _ddb, context.Logger);

        // Persist artifact to S3 + reference in DynamoDB
        var requestId = Guid.NewGuid().ToString("N");
        var artifactKey = await StoreArtifact(requestId, ip, input, formatted, context);
        await StoreLog(requestId, ip, input, formatted, artifactKey, context);

        // Optional: email the result
        if (!string.IsNullOrWhiteSpace(input.EmailTo))
        {
            await SendEmail(input.EmailTo!, formatted, context);
        }

        // Include remaining credits in response
        return JsonResponse(200, new {
            analysis = formatted.Analysis,
            emotional_insight = formatted.EmotionalInsight,
            practical_advice = formatted.PracticalAdvice,
            reassurance = formatted.Reassurance,
            mode_used = formatted.ModeUsed,
            credits_remaining = remainingCredits == -1 ? "unlimited" : remainingCredits.ToString()
        });
    }

    private async Task<APIGatewayProxyResponse> HandleAdminLogin(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Password required" });

        var body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body, Json);
        if (body == null || !body.ContainsKey("password"))
            return JsonResponse(400, new { error = "Password required" });

        var token = await AdminSystem.LoginAdmin(body["password"], _secrets, context.Logger);
        if (token == null)
            return JsonResponse(401, new { error = "Invalid password" });

        return JsonResponse(200, new { token });
    }

    private async Task<APIGatewayProxyResponse> HandleAdminGetUsers(ILambdaContext context)
    {
        var users = await AdminSystem.GetAllUsers(_ddb, context.Logger);
        return JsonResponse(200, new { users });
    }

    private async Task<APIGatewayProxyResponse> HandleAdminDashboard(ILambdaContext context)
    {
        try
        {
            var summary = await AdminSystem.GetDashboardSummary(_ddb, context.Logger);
            return JsonResponse(200, summary);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting dashboard: {ex}");
            // Return empty dashboard on error instead of failing
            return JsonResponse(200, new Dictionary<string, object>
            {
                ["todaysTranslations"] = 0,
                ["todaysPurchases"] = 0,
                ["activeTokens"] = 0,
                ["freeSearchLimit"] = 5
            });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleAdminSetUserCredits(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var pathParts = request.Path?.Split('/') ?? Array.Empty<string>();
        var userIdIndex = Array.IndexOf(pathParts, "users");
        if (userIdIndex < 0 || userIdIndex + 1 >= pathParts.Length)
            return JsonResponse(400, new { error = "Invalid user ID" });

        var userId = pathParts[userIdIndex + 1];

        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Credits amount required" });

        var body = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Body, Json);
        if (body == null || !body.ContainsKey("credits"))
            return JsonResponse(400, new { error = "Credits amount required" });

        var credits = Convert.ToInt32(body["credits"].ToString());
        await CreditSystem.SetUserCredits(userId, credits, _ddb, context.Logger);

        return JsonResponse(200, new { message = "Credits updated", userId, credits });
    }

    private async Task<APIGatewayProxyResponse> HandleAdminGrantUserCredits(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var pathParts = request.Path?.Split('/') ?? Array.Empty<string>();
        var userIdIndex = Array.IndexOf(pathParts, "users");
        if (userIdIndex < 0 || userIdIndex + 1 >= pathParts.Length)
            return JsonResponse(400, new { error = "Invalid user ID" });

        var userId = pathParts[userIdIndex + 1];

        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Credits amount required" });

        var body = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Body, Json);
        if (body == null || !body.ContainsKey("credits"))
            return JsonResponse(400, new { error = "Credits amount required" });

        var creditsToAdd = Convert.ToInt32(body["credits"].ToString());
        await CreditSystem.GrantCredits(userId, creditsToAdd, _ddb, context.Logger);

        var newBalance = await CreditSystem.GetUserCredits(userId, _ddb, context.Logger);
        
        // Notify admin of credit grant (treat as purchase notification)
        await CreditSystem.NotifyCreditPurchase(userId, creditsToAdd, null, _ddb, _ses, _sesFromEmail, context.Logger);

        return JsonResponse(200, new { message = "Credits granted", userId, creditsAdded = creditsToAdd, newBalance });
    }

    private async Task<APIGatewayProxyResponse> HandleAdminSetMyCredits(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Admin's userId is hardcoded or from token (for now, use "admin")
        const string adminUserId = "admin";

        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Credits amount required" });

        var body = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Body, Json);
        if (body == null || !body.ContainsKey("credits"))
            return JsonResponse(400, new { error = "Credits amount required" });

        var credits = Convert.ToInt32(body["credits"].ToString());
        await CreditSystem.SetUserCredits(adminUserId, credits, _ddb, context.Logger);

        return JsonResponse(200, new { message = "Your credits updated", credits });
    }

    private async Task<APIGatewayProxyResponse> HandleContact(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Request body is required." });

        ContactRequest? input;
        try
        {
            input = JsonSerializer.Deserialize<ContactRequest>(request.Body, Json);
        }
        catch
        {
            return JsonResponse(400, new { error = "Invalid JSON body." });
        }

        if (input is null)
            return JsonResponse(400, new { error = "Invalid request." });

        try
        {
            input = input.Validate();
        }
        catch (ClientVisibleException ex)
        {
            return JsonResponse(ex.StatusCode, new { error = ex.Message });
        }

        var contactId = Guid.NewGuid().ToString("N");
        var ip = GetClientIp(request) ?? "unknown";
        var userId = CreditSystem.GetUserId(request, ip);
        var timestamp = DateTimeOffset.UtcNow.ToString("O");

        // Store contact message in DynamoDB
        try
        {
            await _ddb.PutItemAsync(new PutItemRequest
            {
                TableName = "LoveBehaviorTranslatorContacts",
                Item = new Dictionary<string, AttributeValue>
                {
                    ["contactId"] = new AttributeValue { S = contactId },
                    ["email"] = new AttributeValue { S = input.Email },
                    ["subject"] = new AttributeValue { S = input.Subject },
                    ["message"] = new AttributeValue { S = input.Message },
                    ["userId"] = new AttributeValue { S = userId },
                    ["ip"] = new AttributeValue { S = ip },
                    ["createdAt"] = new AttributeValue { S = timestamp },
                    ["status"] = new AttributeValue { S = "new" }, // new, replied, closed
                    ["repliedAt"] = new AttributeValue { S = "" },
                    ["ttl"] = new AttributeValue { N = ((DateTimeOffset.UtcNow.ToUnixTimeSeconds()) + (365 * 24 * 60 * 60)).ToString() } // 1 year TTL
                }
            });

            // Send notification email to admin
            await SendContactNotification(input, contactId, context);

            return JsonResponse(200, new { message = "Contact message received. We'll get back to you soon.", contactId });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error storing contact: {ex}");
            return JsonResponse(500, new { error = "Failed to submit contact message. Please try again." });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleAdminGetContacts(ILambdaContext context)
    {
        try
        {
            var scanResp = await _ddb.ScanAsync(new ScanRequest
            {
                TableName = "LoveBehaviorTranslatorContacts"
            });

            var contacts = new List<Dictionary<string, object>>();
            foreach (var item in scanResp.Items)
            {
                var contact = new Dictionary<string, object>
                {
                    ["contactId"] = item["contactId"].S,
                    ["email"] = item["email"].S,
                    ["subject"] = item["subject"].S,
                    ["message"] = item["message"].S,
                    ["createdAt"] = item["createdAt"].S,
                    ["status"] = item.ContainsKey("status") ? item["status"].S : "new"
                };

                if (item.ContainsKey("userId"))
                    contact["userId"] = item["userId"].S;

                if (item.ContainsKey("repliedAt") && !string.IsNullOrWhiteSpace(item["repliedAt"].S))
                    contact["repliedAt"] = item["repliedAt"].S;

                contacts.Add(contact);
            }

            // Sort by createdAt descending (newest first)
            contacts = contacts.OrderByDescending(c => c["createdAt"].ToString()).ToList();

            return JsonResponse(200, new { contacts });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting contacts: {ex}");
            return JsonResponse(500, new { error = "Failed to get contacts" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleAdminReplyContact(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var pathParts = request.Path?.Split('/') ?? Array.Empty<string>();
        var contactIdIndex = Array.IndexOf(pathParts, "contacts");
        if (contactIdIndex < 0 || contactIdIndex + 1 >= pathParts.Length)
            return JsonResponse(400, new { error = "Invalid contact ID" });

        var contactId = pathParts[contactIdIndex + 1];

        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Reply message required" });

        var body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body, Json);
        if (body == null || !body.ContainsKey("replyMessage"))
            return JsonResponse(400, new { error = "Reply message required" });

        var replyMessage = body["replyMessage"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(replyMessage))
            return JsonResponse(400, new { error = "Reply message cannot be empty" });

        try
        {
            // Get contact details
            var getResp = await _ddb.GetItemAsync(new GetItemRequest
            {
                TableName = "LoveBehaviorTranslatorContacts",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["contactId"] = new AttributeValue { S = contactId }
                }
            });

            if (getResp.Item.Count == 0)
                return JsonResponse(404, new { error = "Contact not found" });

            var contactEmail = getResp.Item["email"].S;
            var originalSubject = getResp.Item["subject"].S;
            var originalMessage = getResp.Item["message"].S;

            // Send reply email
            await SendContactReply(contactEmail, originalSubject, originalMessage, replyMessage, context);

            // Update contact status
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = "LoveBehaviorTranslatorContacts",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["contactId"] = new AttributeValue { S = contactId }
                },
                UpdateExpression = "SET #status = :status, repliedAt = :repliedAt",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = "replied" },
                    [":repliedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                }
            });

            return JsonResponse(200, new { message = "Reply sent successfully", contactId });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error replying to contact: {ex}");
            var errorMessage = ex.Message;
            if (errorMessage.Contains("SES_FROM_EMAIL"))
                errorMessage = "Email service not configured. Please set SES_FROM_EMAIL in Lambda environment variables.";
            else if (errorMessage.Contains("not verified") || errorMessage.Contains("verification"))
                errorMessage = "Email address not verified in SES. Please verify the sender email in Amazon SES.";
            return JsonResponse(500, new { error = errorMessage });
        }
    }

    private async Task SendContactNotification(ContactRequest contact, string contactId, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(_sesFromEmail))
        {
            context.Logger.LogWarning("SES_FROM_EMAIL not set; skipping contact notification.");
            return;
        }

        var subject = $"New Contact Form Submission: {contact.Subject}";
        var body = $@"New contact form submission received:

Contact ID: {contactId}
Email: {contact.Email}
Subject: {contact.Subject}

Message:
{contact.Message}

---
Reply to this contact at: {contact.Email}
";

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _sesFromEmail,
                Destination = new Destination { ToAddresses = new List<string> { _sesFromEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to send contact notification: {ex}");
        }
    }

    private async Task SendContactReply(string toEmail, string originalSubject, string originalMessage, string replyMessage, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(_sesFromEmail))
        {
            context.Logger.LogError("SES_FROM_EMAIL not set; cannot send reply.");
            throw new Exception("SES_FROM_EMAIL not configured");
        }

        var subject = $"Re: {originalSubject}";
        var body = $@"Hello,

Thank you for contacting Love Behavior Translator. Here's our response:

{replyMessage}

---
Original message:
{originalMessage}

Best regards,
Love Behavior Translator Support
";

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _sesFromEmail,
                Destination = new Destination { ToAddresses = new List<string> { toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                },
                ReplyToAddresses = new List<string> { _sesFromEmail }
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to send contact reply: {ex}");
            // Provide more specific error message
            if (ex.Message.Contains("not verified") || ex.Message.Contains("verification"))
                throw new Exception("Email address not verified in SES. Please verify the sender email in Amazon SES.");
            if (ex.Message.Contains("SES_FROM_EMAIL"))
                throw new Exception("SES_FROM_EMAIL not configured in Lambda environment variables.");
            throw new Exception($"Email send failed: {ex.Message}");
        }
    }

    private async Task<string> GetOpenAiApiKey()
    {
        var resp = await _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _openAiSecretArn });
        if (string.IsNullOrWhiteSpace(resp.SecretString))
            throw new Exception("OpenAI secret is empty.");

        // Allow either raw key in SecretString or JSON {"OPENAI_API_KEY":"..."}
        var s = resp.SecretString.Trim();
        if (!s.StartsWith("{")) return s;

        using var doc = JsonDocument.Parse(s);
        if (doc.RootElement.TryGetProperty("OPENAI_API_KEY", out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? throw new Exception("OpenAI key missing in secret JSON.");

        throw new Exception("OpenAI key missing in secret JSON.");
    }

    private async Task<string> CallOpenAi(string prompt, string apiKey, ILambdaContext context)
    {
        // OpenAI Chat Completions (compatible) – minimal, production-safe defaults
        var payload = new
        {
            model = _openAiModel,
            temperature = 0.7,
            max_tokens = 900,
            messages = new object[]
            {
                new { role = "system", content = "You are a compassionate relationship advisor and behavioral analyst." },
                new { role = "user", content = prompt }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if ((int)resp.StatusCode == 429)
            throw new ClientVisibleException(429, "The AI service is busy right now. Please try again in a moment.");

        if (!resp.IsSuccessStatusCode)
        {
            context.Logger.LogError($"OpenAI error {(int)resp.StatusCode}: {body}");
            throw new ClientVisibleException(502, "AI provider error. Please try again later.");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new ClientVisibleException(502, "AI provider returned an empty response.");

        return content.Trim();
    }

    private async Task<bool> RateLimitCheck(string ip, ILambdaContext context)
    {
        // Keyed per minute: pk=RL#<ip>, sk=<yyyyMMddHHmm>
        var now = DateTimeOffset.UtcNow;
        var minuteKey = now.ToString("yyyyMMddHHmm");
        var pk = $"RL#{ip}";
        var sk = minuteKey;
        var ttl = now.AddMinutes(5).ToUnixTimeSeconds(); // keep counters briefly

        // Burst allowance: allow a small number even if table is cold-starting
        // Stored count increments atomically
        var update = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk }
            },
            UpdateExpression = "SET #ttl = if_not_exists(#ttl, :ttl) ADD #count :inc",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#ttl"] = "ttl",
                ["#count"] = "count"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":ttl"] = new AttributeValue { N = ttl.ToString() },
                [":inc"] = new AttributeValue { N = "1" }
            },
            ReturnValues = ReturnValue.UPDATED_NEW
        };

        try
        {
            var resp = await _ddb.UpdateItemAsync(update);
            var newCount = int.Parse(resp.Attributes["count"].N);
            return newCount <= _rateLimitPerMinute + _rateLimitBurst;
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"RateLimitCheck failed (allowing request): {ex}");
            return true; // fail-open to avoid blocking due to infra issues
        }
    }

    private async Task<string> StoreArtifact(string requestId, string ip, BehaviorAnalysisRequest input, BehaviorAnalysisResponse output, ILambdaContext context)
    {
        var key = $"analysis/{DateTimeOffset.UtcNow:yyyy/MM/dd}/{requestId}.json";
        var payload = JsonSerializer.Serialize(new { requestId, ip, input, output, createdAt = DateTimeOffset.UtcNow }, Json);

        try
        {
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = payload,
                ContentType = "application/json"
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to store artifact to S3: {ex}");
            // still return a key for traceability, even if missing
        }

        return key;
    }

    private async Task StoreLog(string requestId, string ip, BehaviorAnalysisRequest input, BehaviorAnalysisResponse output, string artifactKey, ILambdaContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var ttl = now.AddDays(30).ToUnixTimeSeconds();

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"REQ#{requestId}" },
            ["sk"] = new AttributeValue { S = now.ToString("O") },
            ["ttl"] = new AttributeValue { N = ttl.ToString() },
            ["ip"] = new AttributeValue { S = ip },
            ["mode"] = new AttributeValue { S = input.AnalysisMode },
            ["relationshipType"] = new AttributeValue { S = input.RelationshipType ?? "" },
            ["artifactKey"] = new AttributeValue { S = artifactKey },
        };

        try
        {
            await _ddb.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to store log in DynamoDB: {ex}");
        }
    }

    private async Task SendEmail(string toEmail, BehaviorAnalysisResponse output, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(_sesFromEmail))
        {
            context.Logger.LogError("SES_FROM_EMAIL not set; skipping email.");
            return;
        }

        var subject = "Your Love Behavior Translator analysis";
        var body = $@"
Disclaimer: This app provides general relationship insights and is not professional therapy or counseling.

Analysis:
{output.Analysis}

Emotional Insight:
{output.EmotionalInsight}

Practical Advice:
{output.PracticalAdvice}

Reassurance:
{output.Reassurance}
";

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                Source = _sesFromEmail,
                Destination = new Destination { ToAddresses = new List<string> { toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"SES send failed: {ex}");
        }
    }

    private static string? GetClientIp(APIGatewayProxyRequest request)
    {
        // API Gateway (REST) typically sets requestContext.identity.sourceIp
        return request.RequestContext?.Identity?.SourceIp;
    }

    private static APIGatewayProxyResponse JsonResponse(int statusCode, object body)
        => new()
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Headers"] = "Content-Type,Authorization,x-user-id",
                ["Access-Control-Allow-Methods"] = "OPTIONS,GET,POST"
            },
            Body = JsonSerializer.Serialize(body, Json)
        };

    private static string GetEnv(string name)
        => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"Missing required env var: {name}");
}

public sealed class ClientVisibleException : Exception
{
    public int StatusCode { get; }

    public ClientVisibleException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}


