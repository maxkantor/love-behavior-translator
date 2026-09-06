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
using Stripe;
using Stripe.Checkout;

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
    private readonly string _adminEmail;
    private readonly string _stripeSecretKey;

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
        _adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? _sesFromEmail; // Fallback to SES_FROM_EMAIL if not set
        _stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        
        // Initialize Stripe if key is provided
        if (!string.IsNullOrWhiteSpace(_stripeSecretKey))
        {
            StripeConfiguration.ApiKey = _stripeSecretKey;
        }
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
            context.Logger.LogInformation($"Request: {method} {path} (raw: {rawPath})");
            context.Logger.LogInformation($"Request path details: rawPath='{rawPath}', lowerPath='{path}', method='{method}'");
            
            // Log all path components for debugging
            if (rawPath.Contains("stripe", StringComparison.OrdinalIgnoreCase))
            {
                context.Logger.LogInformation($"Stripe-related path detected! rawPath='{rawPath}', path='{path}'");
            }

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

            // Test email endpoint (admin only, for debugging)
            if (method == "POST" && (path.EndsWith("/admin/test-email") || path == "/admin/test-email"))
            {
                if (!AdminSystem.VerifyAdminToken(request, _secrets))
                    return JsonResponse(401, new { error = "Unauthorized" });
                return await HandleTestEmail(context);
            }

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

                if (method == "GET" && (path.EndsWith("/admin/activities") || path == "/admin/activities"))
                    return await HandleAdminGetActivities(context);

                // If we're in /admin but no route matched, return 404
                context.Logger.LogWarning($"Admin route not found: {method} {path}");
                return JsonResponse(404, new { error = $"Admin endpoint not found: {method} {path}" });
            }

            // Get user credits endpoint
            if (method == "GET" && (path.EndsWith("/credits") || path == "/credits"))
                return await HandleGetCredits(request, context);

            // Email verification endpoints
            // Handle both exact match and endsWith for API Gateway proxy resources
            var isSendVerification = method == "POST" && (
                path == "/email/send-verification" || 
                path.EndsWith("/email/send-verification") ||
                rawPath.Contains("/email/send-verification", StringComparison.OrdinalIgnoreCase));
            
            if (isSendVerification)
                return await HandleSendVerificationCode(request, context);

            var isVerifyEmail = method == "POST" && (
                path == "/email/verify" || 
                path.EndsWith("/email/verify") ||
                rawPath.Contains("/email/verify", StringComparison.OrdinalIgnoreCase));
            
            if (isVerifyEmail)
                return await HandleVerifyEmail(request, context);

            var isRestoreCredits = method == "POST" && (
                path == "/credits/restore" || 
                path.EndsWith("/credits/restore") ||
                rawPath.Contains("/credits/restore", StringComparison.OrdinalIgnoreCase));
            
            if (isRestoreCredits)
                return await HandleRestoreCredits(request, context);

            // Contact form endpoint
            if (method == "POST" && (path.EndsWith("/contact") || path == "/contact"))
                return await HandleContact(request, context);

            // Analyze endpoint
            if (method == "POST" && (path.EndsWith("/analyze") || path == "/analyze"))
                return await HandleAnalyze(request, context);

            // Stripe checkout session creation
            // Handle various path formats that API Gateway might send
            var isStripeCheckout = method == "POST" && (
                path.EndsWith("/stripe/create-checkout-session") || 
                path == "/stripe/create-checkout-session" ||
                path.EndsWith("stripe/create-checkout-session") ||
                path == "stripe/create-checkout-session" ||
                rawPath.Contains("/stripe/create-checkout-session", StringComparison.OrdinalIgnoreCase) ||
                rawPath.Contains("stripe/create-checkout-session", StringComparison.OrdinalIgnoreCase));
            
            if (isStripeCheckout)
            {
                context.Logger.LogInformation($"✅ Matched Stripe checkout session endpoint! rawPath='{rawPath}', path='{path}'");
                return await HandleCreateCheckoutSession(request, context);
            }
            
            // Debug logging for unmatched Stripe requests
            if (method == "POST" && (rawPath.Contains("stripe", StringComparison.OrdinalIgnoreCase) || path.Contains("stripe")))
            {
                context.Logger.LogWarning($"⚠️ Stripe-related POST request but didn't match checkout endpoint. rawPath='{rawPath}', path='{path}'");
            }

            // Stripe webhook - handle both POST (webhook events) and GET (verification/health checks)
            var isStripeWebhook = (method == "POST" || method == "GET") && (
                path.EndsWith("/stripe/webhook") || 
                path == "/stripe/webhook" ||
                rawPath.Contains("/stripe/webhook", StringComparison.OrdinalIgnoreCase));
            
            if (isStripeWebhook)
            {
                context.Logger.LogInformation($"✅ Matched Stripe webhook endpoint! Method: {method}, rawPath='{rawPath}', path='{path}'");
                
                // Handle GET requests (Stripe verification or health checks)
                if (method == "GET")
                {
                    context.Logger.LogInformation("Stripe webhook GET request (verification/health check)");
                    return JsonResponse(200, new { status = "ok", message = "Webhook endpoint is active" });
                }
                
                // Handle POST requests (actual webhook events)
                return await HandleStripeWebhook(request, context);
            }

            // Final 404 - log what we received for debugging
            context.Logger.LogWarning($"❌ 404 - No route matched. Method: {method}, Path: {path}, RawPath: {rawPath}");
            return JsonResponse(404, new { error = "Not found", method, path, rawPath });
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
        await CreditSystem.NotifyCreditPurchase(userId, creditsToAdd, null, _ddb, _ses, _sesFromEmail, context.Logger, adminEmail: _adminEmail);

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
            context.Logger.LogInformation($"📝 Storing contact form submission: contactId={contactId}, email={input.Email}, subject={input.Subject}");
            
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

            context.Logger.LogInformation($"✅ Contact stored in DynamoDB. Now sending notification email...");

            // Send notification email to admin
            try
            {
                await SendContactNotification(input, contactId, context);
                context.Logger.LogInformation($"✅ Contact notification process completed.");
            }
            catch (Exception emailEx)
            {
                // Log email error but don't fail the request - contact is already stored
                context.Logger.LogError($"⚠️ Contact stored successfully but email notification failed: {emailEx.GetType().Name}: {emailEx.Message}");
                context.Logger.LogError($"⚠️ Email error details: {emailEx}");
            }

            return JsonResponse(200, new { message = "Contact message received. We'll get back to you soon.", contactId });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"❌ Error storing contact: {ex.GetType().Name}: {ex.Message}");
            context.Logger.LogError($"❌ Stack trace: {ex.StackTrace}");
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

    private async Task<APIGatewayProxyResponse> HandleTestEmail(ILambdaContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_sesFromEmail))
            {
                return JsonResponse(400, new { error = "SES_FROM_EMAIL not set in Lambda environment variables" });
            }

            context.Logger.LogInformation($"📧 Test email: Sending test email to {_sesFromEmail}");

            var subject = "🧪 Test Email - Love Behavior Translator";
            var body = $@"This is a test email from Love Behavior Translator.

If you received this, SES email sending is working correctly!

Timestamp: {DateTimeOffset.UtcNow:O}
SES_FROM_EMAIL: {_sesFromEmail}
";

            var request = new SendEmailRequest
            {
                Source = _sesFromEmail,
                Destination = new Destination { ToAddresses = new List<string> { _sesFromEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            };

            context.Logger.LogInformation($"📧 Test email: Calling SES.SendEmailAsync");
            var response = await _ses.SendEmailAsync(request);
            context.Logger.LogInformation($"✅ Test email: SES.SendEmailAsync succeeded! MessageId={response.MessageId}");

            return JsonResponse(200, new { 
                success = true, 
                message = "Test email sent successfully",
                messageId = response.MessageId,
                to = _sesFromEmail
            });
        }
        catch (Amazon.SimpleEmail.Model.MessageRejectedException ex)
        {
            context.Logger.LogError($"❌ Test email: MessageRejectedException: {ex.Message}");
            return JsonResponse(400, new { 
                error = "Email rejected by SES", 
                message = ex.Message,
                errorCode = ex.ErrorCode,
                statusCode = ex.StatusCode
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"❌ Test email: Exception: {ex.GetType().Name}: {ex.Message}");
            context.Logger.LogError($"❌ Test email: Stack trace: {ex.StackTrace}");
            return JsonResponse(500, new { 
                error = "Failed to send test email", 
                message = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleAdminGetActivities(ILambdaContext context)
    {
        try
        {
            var activities = await AdminSystem.GetPurchaseActivities(_ddb, context.Logger);
            return JsonResponse(200, new { activities });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting activities: {ex}");
            return JsonResponse(500, new { error = "Failed to get activities" });
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

            // Update contact status (allow replying multiple times)
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = "LoveBehaviorTranslatorContacts",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["contactId"] = new AttributeValue { S = contactId }
                },
                UpdateExpression = "SET #status = :status, repliedAt = :repliedAt, replyMessage = :replyMessage",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = "replied" },
                    [":repliedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
                    [":replyMessage"] = new AttributeValue { S = replyMessage }
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
        context.Logger.LogInformation($"📧 SendContactNotification called: contactId={contactId}, email={contact.Email}, subject={contact.Subject}");
        
        if (string.IsNullOrWhiteSpace(_sesFromEmail))
        {
            context.Logger.LogWarning("❌ SES_FROM_EMAIL not set; skipping contact notification.");
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
            var adminEmailToUse = string.IsNullOrWhiteSpace(_adminEmail) ? _sesFromEmail : _adminEmail;
            context.Logger.LogInformation($"📧 Preparing contact notification email: From={_sesFromEmail}, To={adminEmailToUse}, Subject={subject}");
            
            var request = new SendEmailRequest
            {
                Source = _sesFromEmail,
                Destination = new Destination { ToAddresses = new List<string> { adminEmailToUse } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            };
            
            context.Logger.LogInformation($"📧 Calling SES.SendEmailAsync for contact notification");
            var response = await _ses.SendEmailAsync(request);
            
            context.Logger.LogInformation($"✅ Contact notification email sent successfully! MessageId={response.MessageId}, HttpStatusCode={response.HttpStatusCode}");
        }
        catch (Amazon.SimpleEmail.Model.MessageRejectedException ex)
        {
            context.Logger.LogError($"❌ Contact notification: SES MessageRejectedException: {ex.Message}");
            context.Logger.LogError($"❌ Error Code: {ex.ErrorCode}, Status Code: {ex.StatusCode}");
            if (ex.InnerException != null)
            {
                context.Logger.LogError($"❌ Inner Exception: {ex.InnerException}");
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"❌ Failed to send contact notification: {ex.GetType().Name}: {ex.Message}");
            context.Logger.LogError($"❌ Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                context.Logger.LogError($"❌ Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
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

    private async Task StorePurchaseActivity(string userId, string customerName, string customerEmail, int credits, decimal amount, string? cardLast4, string paymentId, string sessionId, DateTimeOffset purchaseDate, ILambdaContext context)
    {
        var activityId = $"PURCHASE#{sessionId}";
        var ttl = purchaseDate.AddYears(2).ToUnixTimeSeconds(); // Keep for 2 years

        var item = new Dictionary<string, AttributeValue>
        {
            ["activityId"] = new AttributeValue { S = activityId },
            ["userId"] = new AttributeValue { S = userId },
            ["activityType"] = new AttributeValue { S = "purchase" },
            ["customerName"] = new AttributeValue { S = customerName },
            ["customerEmail"] = new AttributeValue { S = customerEmail },
            ["credits"] = new AttributeValue { N = credits.ToString() },
            ["amount"] = new AttributeValue { N = amount.ToString("F2") },
            ["paymentId"] = new AttributeValue { S = paymentId },
            ["sessionId"] = new AttributeValue { S = sessionId },
            ["purchaseDate"] = new AttributeValue { S = purchaseDate.ToString("O") },
            ["createdAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
            ["ttl"] = new AttributeValue { N = ttl.ToString() }
        };

        if (!string.IsNullOrWhiteSpace(cardLast4))
        {
            item["cardLast4"] = new AttributeValue { S = cardLast4 };
        }

        try
        {
            await _ddb.PutItemAsync(new PutItemRequest
            {
                TableName = "LoveBehaviorTranslatorActivities",
                Item = item
            });
            context.Logger.LogInformation($"✅ Stored purchase activity: {activityId} for user {userId}");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to store purchase activity in DynamoDB: {ex}");
            throw;
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

    private async Task<APIGatewayProxyResponse> HandleCreateCheckoutSession(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(_stripeSecretKey))
        {
            context.Logger.LogError("STRIPE_SECRET_KEY not set in Lambda environment variables");
            return JsonResponse(500, new { error = "Stripe not configured. Please set STRIPE_SECRET_KEY in Lambda environment variables." });
        }

        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Request body required" });

        CreateCheckoutSessionRequest? input;
        try
        {
            input = JsonSerializer.Deserialize<CreateCheckoutSessionRequest>(request.Body, Json);
        }
        catch
        {
            return JsonResponse(400, new { error = "Invalid JSON body" });
        }

        if (input == null || input.Credits <= 0 || input.Price <= 0)
        {
            context.Logger.LogWarning($"Invalid checkout session request: credits={input?.Credits}, price={input?.Price}");
            return JsonResponse(400, new { error = "Invalid request: credits and price must be greater than 0" });
        }

        var ip = GetClientIp(request) ?? "unknown";
        var userId = CreditSystem.GetUserId(request, ip);
        
        // Log userId source for debugging
        var userIdSource = request.Headers?.ContainsKey("x-user-id") == true ? "header" : "ip-based";
        context.Logger.LogInformation($"Creating Stripe checkout session: {input.Credits} credits for ${input.Price}, userId: {userId} (source: {userIdSource})");

        try
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"{input.Credits} Deep Relationship Readings",
                                Description = "Get clarity on your relationship with AI-powered behavioral analysis"
                            },
                            UnitAmount = (long)(input.Price * 100) // Convert to cents
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = input.SuccessUrl ?? "https://lovebehaviortranslator.com/?payment=success",
                CancelUrl = input.CancelUrl ?? "https://lovebehaviortranslator.com/?payment=cancelled",
                Metadata = new Dictionary<string, string>
                {
                    ["app"] = "lovebehaviortranslator",
                    ["product"] = "lovebehaviortranslator",
                    ["userId"] = userId,
                    ["credits"] = input.Credits.ToString(),
                    ["price"] = input.Price.ToString("F2")
                },
                CustomerEmail = input.Email // Optional: pre-fill email
            };
            
            context.Logger.LogInformation($"Stripe session metadata: userId={userId}, credits={input.Credits}, price=${input.Price:F2}");

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            context.Logger.LogInformation($"Stripe checkout session created: {session.Id}, URL: {session.Url}");
            
            if (string.IsNullOrWhiteSpace(session.Url))
            {
                context.Logger.LogError($"Stripe session created but URL is empty: {session.Id}");
                return JsonResponse(500, new { error = "Checkout session created but URL is missing" });
            }

            return JsonResponse(200, new { sessionId = session.Id, url = session.Url });
        }
        catch (StripeException ex)
        {
            context.Logger.LogError($"Stripe error creating checkout session: {ex}");
            return JsonResponse(500, new { error = $"Stripe error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error creating checkout session: {ex}");
            return JsonResponse(500, new { error = "Failed to create checkout session" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleStripeWebhook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("🔔 Stripe webhook received!");
        context.Logger.LogInformation($"Request body length: {request.Body?.Length ?? 0}");
        context.Logger.LogInformation($"Headers: {string.Join(", ", request.Headers?.Keys ?? Array.Empty<string>())}");
        
        // Log all header keys for debugging
        if (request.Headers != null)
        {
            foreach (var header in request.Headers)
            {
                context.Logger.LogInformation($"Header: {header.Key} = {header.Value?.Substring(0, Math.Min(50, header.Value?.Length ?? 0))}...");
            }
        }
        
        // Also check MultiValueHeaders (API Gateway sometimes uses this)
        if (request.MultiValueHeaders != null)
        {
            foreach (var header in request.MultiValueHeaders)
            {
                context.Logger.LogInformation($"MultiValueHeader: {header.Key} = {string.Join(", ", header.Value ?? Array.Empty<string>())}");
            }
        }
        
        if (string.IsNullOrWhiteSpace(_stripeSecretKey))
        {
            context.Logger.LogError("❌ STRIPE_SECRET_KEY not configured");
            return JsonResponse(500, new { error = "Stripe not configured" });
        }

        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            context.Logger.LogWarning("⚠️ STRIPE_WEBHOOK_SECRET not set; webhook verification skipped (INSECURE - only for testing)");
        }

        var body = request.Body ?? "";
        
        // Try to get signature from headers (case-insensitive)
        string? signature = null;
        if (request.Headers != null)
        {
            // Check case-insensitively
            var headerKey = request.Headers.Keys.FirstOrDefault(k => 
                k.Equals("stripe-signature", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("Stripe-Signature", StringComparison.OrdinalIgnoreCase));
            
            if (headerKey != null)
            {
                signature = request.Headers[headerKey];
            }
        }
        
        // Also check MultiValueHeaders (API Gateway sometimes uses this)
        if (signature == null && request.MultiValueHeaders != null)
        {
            var multiHeaderKey = request.MultiValueHeaders.Keys.FirstOrDefault(k => 
                k.Equals("stripe-signature", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("Stripe-Signature", StringComparison.OrdinalIgnoreCase));
            
            if (multiHeaderKey != null && request.MultiValueHeaders[multiHeaderKey]?.Count > 0)
            {
                signature = request.MultiValueHeaders[multiHeaderKey][0];
            }
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            context.Logger.LogError("❌ Missing stripe-signature header");
            context.Logger.LogError($"Available headers: {string.Join(", ", request.Headers?.Keys ?? Array.Empty<string>())}");
            return JsonResponse(400, new { error = "Missing stripe-signature header" });
        }
        
        context.Logger.LogInformation($"✅ Stripe signature present: {signature.Substring(0, Math.Min(20, signature.Length))}...");

        try
        {
            Event stripeEvent;
            if (!string.IsNullOrWhiteSpace(webhookSecret))
            {
                // Disable API version mismatch exception to handle newer Stripe API versions
                stripeEvent = EventUtility.ConstructEvent(body, signature, webhookSecret, throwOnApiVersionMismatch: false);
            }
            else
            {
                // In development, parse without verification (not recommended for production)
                // Disable API version mismatch exception
                var options = new JsonSerializerOptions(Json);
                stripeEvent = JsonSerializer.Deserialize<Event>(body, options) ?? throw new Exception("Failed to parse event");
            }

            // Handle the event
            context.Logger.LogInformation($"Processing Stripe webhook event: {stripeEvent.Type}, ID: {stripeEvent.Id}");
            
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;
                context.Logger.LogInformation($"Checkout session completed: {session?.Id}, Payment status: {session?.PaymentStatus}");

                // Shared Stripe account: ignore checkouts that belong to Lucky Numbers Lab or other apps
                if (session?.Metadata != null)
                {
                    if (session.Metadata.TryGetValue("app", out var appMeta) &&
                        !string.Equals(appMeta, "lovebehaviortranslator", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Logger.LogInformation($"Ignoring foreign app session {session.Id} (app={appMeta})");
                        return JsonResponse(200, new { received = true, ignored = true, reason = "foreign_app" });
                    }
                    if (session.Metadata.TryGetValue("product", out var productMeta) &&
                        !string.Equals(productMeta, "lovebehaviortranslator", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Logger.LogInformation($"Ignoring foreign product session {session.Id} (product={productMeta})");
                        return JsonResponse(200, new { received = true, ignored = true, reason = "foreign_product" });
                    }
                }

                var successUrl = session?.SuccessUrl ?? "";
                var cancelUrl = session?.CancelUrl ?? "";
                var combinedUrls = $"{successUrl}\n{cancelUrl}".ToLowerInvariant();
                var foreignUrlMarkers = new[] { "luckynumberslab", "hybridrace", "jobcompass", "youtubebooster", "gohyrox", "ywux87cqah" };
                if (foreignUrlMarkers.Any(m => combinedUrls.Contains(m)))
                {
                    context.Logger.LogInformation($"Ignoring foreign URL session {session?.Id} (url={successUrl})");
                    return JsonResponse(200, new { received = true, ignored = true, reason = "foreign_url" });
                }
                
                if (session?.Metadata != null)
                {
                    context.Logger.LogInformation($"Session metadata: {string.Join(", ", session.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    
                    if (session.Metadata.ContainsKey("userId") && session.Metadata.ContainsKey("credits"))
                    {
                        var userId = session.Metadata["userId"];
                        var credits = int.Parse(session.Metadata["credits"]);
                        var amountPaid = session.Metadata.ContainsKey("price") 
                            ? decimal.Parse(session.Metadata["price"]) 
                            : (decimal?)null;

                        context.Logger.LogInformation($"Granting {credits} credits to user {userId} from Stripe payment {session.Id}");

                        // Grant credits to user
                        await CreditSystem.GrantCredits(userId, credits, _ddb, context.Logger);
                        
                        // Verify credits were granted
                        var newBalance = await CreditSystem.GetUserCredits(userId, _ddb, context.Logger);
                        context.Logger.LogInformation($"Credits granted successfully. New balance for {userId}: {newBalance}");
                        
                        // Fetch additional customer details from Stripe
                        string? customerName = null;
                        string? customerEmail = null;
                        string? cardLast4 = null;
                        var purchaseDate = DateTimeOffset.UtcNow;
                        
                        try
                        {
                            // First, try to get email and name from session directly (most reliable)
                            customerEmail = session.CustomerEmail;
                            context.Logger.LogInformation($"Session CustomerEmail: {customerEmail}");
                            
                            // Check CustomerDetails on session (contains name and email from checkout)
                            if (session.CustomerDetails != null)
                            {
                                if (!string.IsNullOrWhiteSpace(session.CustomerDetails.Email))
                                {
                                    customerEmail = session.CustomerDetails.Email;
                                    context.Logger.LogInformation($"Using CustomerDetails.Email: {customerEmail}");
                                }
                                if (!string.IsNullOrWhiteSpace(session.CustomerDetails.Name))
                                {
                                    customerName = session.CustomerDetails.Name;
                                    context.Logger.LogInformation($"Using CustomerDetails.Name: {customerName}");
                                }
                            }
                            
                            // Get customer name and email from Stripe Customer object if available
                            if (!string.IsNullOrWhiteSpace(session.CustomerId))
                            {
                                try
                                {
                                    var customerService = new Stripe.CustomerService();
                                    var customer = await customerService.GetAsync(session.CustomerId);
                                    if (!string.IsNullOrWhiteSpace(customer.Name))
                                    {
                                        customerName = customer.Name;
                                        context.Logger.LogInformation($"Retrieved customer name from Customer object: {customerName}");
                                    }
                                    if (!string.IsNullOrWhiteSpace(customer.Email))
                                    {
                                        customerEmail = customer.Email;
                                        context.Logger.LogInformation($"Retrieved customer email from Customer object: {customerEmail}");
                                    }
                                }
                                catch (Exception customerEx)
                                {
                                    context.Logger.LogWarning($"Could not fetch customer object: {customerEx.Message}");
                                }
                            }
                            
                            // If we still don't have email, try to get it from payment intent
                            if (string.IsNullOrWhiteSpace(customerEmail) && !string.IsNullOrWhiteSpace(session.PaymentIntentId))
                            {
                                try
                                {
                                    var paymentIntentService = new Stripe.PaymentIntentService();
                                    var paymentIntent = await paymentIntentService.GetAsync(session.PaymentIntentId);
                                    if (!string.IsNullOrWhiteSpace(paymentIntent.ReceiptEmail))
                                    {
                                        customerEmail = paymentIntent.ReceiptEmail;
                                        context.Logger.LogInformation($"Using payment intent receipt email: {customerEmail}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    context.Logger.LogWarning($"Could not get email from payment intent: {ex.Message}");
                                }
                            }
                            
                            context.Logger.LogInformation($"Final customer info - Name: {customerName ?? "null"}, Email: {customerEmail ?? "null"}");
                            
                            // Link email to visitor ID if email is available (automatic linking on purchase)
                            if (!string.IsNullOrWhiteSpace(customerEmail))
                            {
                                try
                                {
                                    await CreditSystem.LinkEmailToVisitorId(customerEmail, userId, _ddb, context.Logger);
                                    context.Logger.LogInformation($"✅ Automatically linked email {customerEmail} to visitor {userId} after purchase");
                                }
                                catch (Exception linkEx)
                                {
                                    // Log but don't fail - credits are already granted
                                    context.Logger.LogWarning($"⚠️ Failed to link email after purchase (non-critical): {linkEx.Message}");
                                }
                            }
                            
                            // Get payment method last 4 digits from PaymentIntent
                            if (!string.IsNullOrWhiteSpace(session.PaymentIntentId))
                            {
                                var paymentIntentService = new Stripe.PaymentIntentService();
                                var paymentIntent = await paymentIntentService.GetAsync(session.PaymentIntentId);
                                
                                if (paymentIntent.PaymentMethodId != null)
                                {
                                    var paymentMethodService = new Stripe.PaymentMethodService();
                                    var paymentMethod = await paymentMethodService.GetAsync(paymentIntent.PaymentMethodId);
                                    if (paymentMethod.Card != null)
                                    {
                                        cardLast4 = paymentMethod.Card.Last4;
                                        context.Logger.LogInformation($"Retrieved card last 4: {cardLast4}");
                                    }
                                }
                                
                                // Use session created date if available
                                if (session.Created != default)
                                {
                                    purchaseDate = new DateTimeOffset(session.Created, TimeSpan.Zero);
                                }
                            }
                        }
                        catch (Exception stripeEx)
                        {
                            context.Logger.LogWarning($"⚠️ Could not fetch additional Stripe details (continuing anyway): {stripeEx.Message}");
                        }
                        
                        // Store purchase activity in DynamoDB
                        try
                        {
                            // Use customer name, or fallback to email, or "Unknown"
                            var finalCustomerName = customerName ?? customerEmail ?? "Unknown";
                            // Use customer email from any source, or empty string
                            var finalCustomerEmail = customerEmail ?? session.CustomerEmail ?? "";
                            
                            context.Logger.LogInformation($"Storing purchase activity: Name={finalCustomerName}, Email={finalCustomerEmail}");
                            
                            await StorePurchaseActivity(
                                userId,
                                finalCustomerName,
                                finalCustomerEmail,
                                credits,
                                amountPaid ?? 0,
                                cardLast4,
                                session.PaymentIntentId ?? session.Id,
                                session.Id,
                                purchaseDate,
                                context
                            );
                            context.Logger.LogInformation($"✅ Purchase activity stored in DynamoDB");
                        }
                        catch (Exception storeEx)
                        {
                            context.Logger.LogError($"⚠️ Failed to store purchase activity (continuing): {storeEx}");
                        }
                        
                        // Notify admin via email
                        try
                        {
                            var emailForNotification = customerEmail ?? session.CustomerEmail;
                            var paymentIntentId = session.PaymentIntentId;
                            
                            await CreditSystem.NotifyCreditPurchase(
                                userId, 
                                credits, 
                                amountPaid, 
                                _ddb, 
                                _ses, 
                                _sesFromEmail, 
                                context.Logger,
                                customerEmail: emailForNotification,
                                paymentId: paymentIntentId,
                                sessionId: session.Id,
                                adminEmail: _adminEmail,
                                customerName: customerName,
                                cardLast4: cardLast4,
                                purchaseDate: purchaseDate
                            );
                            context.Logger.LogInformation($"✅ Admin notification sent for credit purchase: {credits} credits by {userId}");
                        }
                        catch (Exception notifyEx)
                        {
                            context.Logger.LogError($"⚠️ Failed to send admin notification (credits still granted): {notifyEx}");
                        }
                        
                        context.Logger.LogInformation($"✅ Credits granted: {credits} to user {userId} from Stripe payment {session.Id}. New balance: {newBalance}");
                    }
                    else
                    {
                        context.Logger.LogWarning($"⚠️ Session metadata missing userId or credits. Metadata keys: {string.Join(", ", session.Metadata.Keys)}");
                    }
                }
                else
                {
                    context.Logger.LogWarning($"⚠️ Session has no metadata. Session ID: {session?.Id}");
                }
            }
            else
            {
                context.Logger.LogInformation($"Webhook event type '{stripeEvent.Type}' not handled (only processing checkout.session.completed)");
            }

            return JsonResponse(200, new { received = true });
        }
        catch (StripeException ex)
        {
            context.Logger.LogError($"Stripe webhook error: {ex}");
            return JsonResponse(400, new { error = $"Webhook error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error processing webhook: {ex}");
            return JsonResponse(500, new { error = "Failed to process webhook" });
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

    private async Task<APIGatewayProxyResponse> HandleSendVerificationCode(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Request body required" });

        Dictionary<string, string>? body;
        try
        {
            body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body, Json);
        }
        catch
        {
            return JsonResponse(400, new { error = "Invalid JSON body" });
        }

        if (body == null || !body.ContainsKey("email") || string.IsNullOrWhiteSpace(body["email"]))
            return JsonResponse(400, new { error = "Email is required" });

        var email = body["email"].Trim();
        
        // Basic email validation
        if (!email.Contains("@") || email.Length > 254)
            return JsonResponse(400, new { error = "Invalid email address" });

        try
        {
            var code = await CreditSystem.GenerateVerificationCode(email, _ddb, context.Logger);
            await CreditSystem.SendVerificationEmail(email, code, _ses, _sesFromEmail, context.Logger);
            
            return JsonResponse(200, new { message = "Verification code sent to your email" });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error sending verification code: {ex}");
            if (ex.Message.Contains("not verified") || ex.Message.Contains("verification"))
                return JsonResponse(400, new { error = "Email service not configured. Please contact support." });
            return JsonResponse(500, new { error = "Failed to send verification code" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleVerifyEmail(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Request body required" });

        Dictionary<string, string>? body;
        try
        {
            body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body, Json);
        }
        catch
        {
            return JsonResponse(400, new { error = "Invalid JSON body" });
        }

        if (body == null || !body.ContainsKey("email") || !body.ContainsKey("code"))
            return JsonResponse(400, new { error = "Email and code are required" });

        var email = body["email"].Trim();
        var code = body["code"].Trim();
        var ip = GetClientIp(request) ?? "unknown";
        var visitorId = CreditSystem.GetUserId(request, ip);

        try
        {
            var verified = await CreditSystem.VerifyCodeAndLinkEmail(email, code, visitorId, _ddb, context.Logger);
            if (!verified)
                return JsonResponse(400, new { error = "Invalid or expired verification code" });

            // After verification, merge credits from all linked visitor IDs
            var mergedCredits = await CreditSystem.MergeCreditsFromEmail(email, visitorId, _ddb, context.Logger);

            return JsonResponse(200, new { 
                message = "Email verified successfully", 
                credits = mergedCredits,
                userId = visitorId
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error verifying email: {ex}");
            return JsonResponse(500, new { error = "Failed to verify email" });
        }
    }

    private async Task<APIGatewayProxyResponse> HandleRestoreCredits(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return JsonResponse(400, new { error = "Request body required" });

        Dictionary<string, string>? body;
        try
        {
            body = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body, Json);
        }
        catch
        {
            return JsonResponse(400, new { error = "Invalid JSON body" });
        }

        if (body == null || !body.ContainsKey("email") || string.IsNullOrWhiteSpace(body["email"]))
            return JsonResponse(400, new { error = "Email is required" });

        var email = body["email"].Trim();
        var ip = GetClientIp(request) ?? "unknown";
        var visitorId = CreditSystem.GetUserId(request, ip);

        try
        {
            // Check if email is already linked (user might have verified before)
            var linkedVisitorIds = await CreditSystem.GetLinkedVisitorIds(email, _ddb, context.Logger);
            
            if (linkedVisitorIds.Count == 0)
            {
                // Email not linked - need to verify first
                return JsonResponse(400, new { 
                    error = "Email not verified", 
                    requiresVerification = true 
                });
            }

            // Link current visitor ID if not already linked
            await CreditSystem.LinkEmailToVisitorId(email, visitorId, _ddb, context.Logger);

            // Merge credits from all linked visitor IDs
            var mergedCredits = await CreditSystem.MergeCreditsFromEmail(email, visitorId, _ddb, context.Logger);

            return JsonResponse(200, new { 
                message = "Credits restored successfully", 
                credits = mergedCredits,
                userId = visitorId
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error restoring credits: {ex}");
            return JsonResponse(500, new { error = "Failed to restore credits" });
        }
    }

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


