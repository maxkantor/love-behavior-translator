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
            var path = (request.Path ?? "").TrimEnd('/').ToLowerInvariant();
            var method = (request.HttpMethod ?? "GET").ToUpperInvariant();

            if (method == "GET" && (path.EndsWith("/health") || path == "/health"))
                return JsonResponse(200, new { status = "healthy" });

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

    private async Task<APIGatewayProxyResponse> HandleAnalyze(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var ip = GetClientIp(request) ?? "unknown";

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

        // Persist artifact to S3 + reference in DynamoDB
        var requestId = Guid.NewGuid().ToString("N");
        var artifactKey = await StoreArtifact(requestId, ip, input, formatted, context);
        await StoreLog(requestId, ip, input, formatted, artifactKey, context);

        // Optional: email the result
        if (!string.IsNullOrWhiteSpace(input.EmailTo))
        {
            await SendEmail(input.EmailTo!, formatted, context);
        }

        return JsonResponse(200, formatted);
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
                ["Access-Control-Allow-Headers"] = "Content-Type,Authorization",
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


