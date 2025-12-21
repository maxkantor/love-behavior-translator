using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace LoveBehaviorTranslator.Function;

public static class AdminSystem
{
    private const string AdminSecretName = "love-behavior-translator/app-secrets";
    private const string UsersTableName = "LoveBehaviorTranslatorUsers";

    /// <summary>
    /// Verify admin password and return JWT token if valid.
    /// </summary>
    public static async Task<string?> LoginAdmin(string password, IAmazonSecretsManager secrets, ILambdaLogger logger)
    {
        try
        {
            var secretResp = await secrets.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = AdminSecretName
            });

            if (string.IsNullOrWhiteSpace(secretResp.SecretString))
            {
                logger.LogError("Admin secret not found in Secrets Manager");
                return null;
            }

            // Parse JSON secret to get ADMIN_PASSWORD
            var secretJson = System.Text.Json.JsonDocument.Parse(secretResp.SecretString);
            if (!secretJson.RootElement.TryGetProperty("ADMIN_PASSWORD", out var passwordElement))
            {
                logger.LogError("ADMIN_PASSWORD not found in secret");
                return null;
            }

            var storedPassword = passwordElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                logger.LogError("ADMIN_PASSWORD is empty in secret");
                return null;
            }

            // Simple password comparison (in production, use hashed passwords)
            if (password != storedPassword)
                return null;

            // Generate simple JWT token (in production, use proper JWT library)
            var token = GenerateSimpleToken();
            return token;
        }
        catch (Exception ex)
        {
            logger.LogError($"Admin login error: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Verify admin token from Authorization header.
    /// </summary>
    public static bool VerifyAdminToken(APIGatewayProxyRequest request, IAmazonSecretsManager secrets)
    {
        try
        {
            if (request.Headers?.ContainsKey("Authorization") != true)
                return false;

            var authHeader = request.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                return false;

            var token = authHeader.Substring(7).Trim();
            // Simple token verification (in production, use proper JWT verification)
            // For now, just check if it's a valid format
            return token.Length > 20;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all users with their credit balances.
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> GetAllUsers(IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var scanResp = await ddb.ScanAsync(new ScanRequest
            {
                TableName = UsersTableName
            });

            var users = new List<Dictionary<string, object>>();
            foreach (var item in scanResp.Items)
            {
                var user = new Dictionary<string, object>
                {
                    ["userId"] = item["userId"].S
                };

                if (item.ContainsKey("credits"))
                    user["credits"] = int.Parse(item["credits"].N);
                else
                    user["credits"] = 0;

                if (item.ContainsKey("createdAt"))
                    user["createdAt"] = item["createdAt"].S;

                if (item.ContainsKey("lastAnalysisAt"))
                    user["lastAnalysisAt"] = item["lastAnalysisAt"].S;

                if (item.ContainsKey("totalAnalyses"))
                    user["totalAnalyses"] = int.Parse(item["totalAnalyses"].N);
                else
                    user["totalAnalyses"] = 0;

                users.Add(user);
            }

            return users.OrderByDescending(u => u.ContainsKey("createdAt") ? u["createdAt"].ToString() : "").ToList();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting all users: {ex}");
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Get dashboard summary statistics.
    /// </summary>
    public static async Task<Dictionary<string, object>> GetDashboardSummary(IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var today = DateTimeOffset.UtcNow.Date;
            var todayStr = today.ToString("yyyy-MM-dd");

            // Get today's analyses from logs table
            var scanResp = await ddb.ScanAsync(new ScanRequest
            {
                TableName = "LoveBehaviorTranslator",
                FilterExpression = "begins_with(pk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":prefix"] = new AttributeValue { S = "REQ#" }
                }
            });

            var todayAnalyses = 0;
            var todayPurchases = 0; // TODO: Track purchases
            var totalUsers = 0;

            foreach (var item in scanResp.Items)
            {
                if (item.ContainsKey("sk"))
                {
                    var sk = item["sk"].S;
                    if (sk.StartsWith(todayStr))
                        todayAnalyses++;
                }
            }

            // Count users
            var usersScan = await ddb.ScanAsync(new ScanRequest
            {
                TableName = UsersTableName,
                Select = Select.COUNT
            });
            totalUsers = usersScan.Count;

            return new Dictionary<string, object>
            {
                ["todaysTranslations"] = todayAnalyses,
                ["todaysPurchases"] = todayPurchases,
                ["activeTokens"] = totalUsers, // Approximate
                ["freeSearchLimit"] = 5 // Configurable
            };
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting dashboard summary: {ex}");
            return new Dictionary<string, object>
            {
                ["todaysTranslations"] = 0,
                ["todaysPurchases"] = 0,
                ["activeTokens"] = 0,
                ["freeSearchLimit"] = 5
            };
        }
    }

    /// <summary>
    /// Get all purchase activities from DynamoDB.
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> GetPurchaseActivities(IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var scanResp = await ddb.ScanAsync(new ScanRequest
            {
                TableName = "LoveBehaviorTranslatorActivities",
                FilterExpression = "activityType = :type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":type"] = new AttributeValue { S = "purchase" }
                }
            });

            var activities = new List<Dictionary<string, object>>();
            foreach (var item in scanResp.Items)
            {
                var activity = new Dictionary<string, object>
                {
                    ["activityId"] = item["activityId"].S,
                    ["userId"] = item["userId"].S,
                    ["customerName"] = item.ContainsKey("customerName") ? item["customerName"].S : "",
                    ["customerEmail"] = item.ContainsKey("customerEmail") ? item["customerEmail"].S : "",
                    ["credits"] = int.Parse(item["credits"].N),
                    ["amount"] = decimal.Parse(item["amount"].N),
                    ["paymentId"] = item["paymentId"].S,
                    ["sessionId"] = item["sessionId"].S,
                    ["purchaseDate"] = item["purchaseDate"].S
                };

                if (item.ContainsKey("cardLast4"))
                {
                    activity["cardLast4"] = item["cardLast4"].S;
                }

                activities.Add(activity);
            }

            return activities.OrderByDescending(a => a["purchaseDate"].ToString()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting purchase activities: {ex}");
            return new List<Dictionary<string, object>>();
        }
    }

    private static string GenerateSimpleToken()
    {
        // Simple token generation (in production, use proper JWT)
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}

