using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace LoveBehaviorTranslator.Function;

public static class CreditSystem
{
    private const string UsersTableName = "LoveBehaviorTranslatorUsers";
    private const int FreeTierCredits = 5;

    /// <summary>
    /// Get or create user and return their credit balance.
    /// </summary>
    public static async Task<int> GetUserCredits(string userId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var resp = await ddb.GetItemAsync(new GetItemRequest
            {
                TableName = UsersTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = userId }
                }
            });

            if (resp.Item.Count > 0 && resp.Item.ContainsKey("credits"))
            {
                return int.Parse(resp.Item["credits"].N);
            }

            // New user - grant free tier credits
            await SetUserCredits(userId, FreeTierCredits, ddb, logger);
            return FreeTierCredits;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting user credits: {ex}");
            // Fail open - allow request if we can't check credits
            return FreeTierCredits;
        }
    }

    /// <summary>
    /// Check if user has credits (or is admin with unlimited).
    /// </summary>
    public static async Task<bool> HasCredits(string userId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        var credits = await GetUserCredits(userId, ddb, logger);
        // -1 means unlimited (admin)
        return credits == -1 || credits > 0;
    }

    /// <summary>
    /// Deduct 1 credit from user (unless unlimited).
    /// </summary>
    public static async Task<bool> DeductCredit(string userId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var current = await GetUserCredits(userId, ddb, logger);
            
            // Unlimited (admin) - don't deduct
            if (current == -1) return true;
            
            // No credits
            if (current <= 0) return false;

            // Deduct 1 credit
            await ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = UsersTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = userId }
                },
                UpdateExpression = "SET credits = credits - :dec, lastAnalysisAt = :now, totalAnalyses = if_not_exists(totalAnalyses, :zero) + :inc",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":dec"] = new AttributeValue { N = "1" },
                    [":now"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
                    [":zero"] = new AttributeValue { N = "0" },
                    [":inc"] = new AttributeValue { N = "1" }
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error deducting credit: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Set user credits to exact amount (replace).
    /// </summary>
    public static async Task SetUserCredits(string userId, int credits, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            await ddb.PutItemAsync(new PutItemRequest
            {
                TableName = UsersTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["userId"] = new AttributeValue { S = userId },
                    ["credits"] = new AttributeValue { N = credits.ToString() },
                    ["updatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error setting user credits: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Grant credits to user (add to existing).
    /// </summary>
    public static async Task GrantCredits(string userId, int creditsToAdd, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var current = await GetUserCredits(userId, ddb, logger);
            var newTotal = current == -1 ? -1 : current + creditsToAdd; // Keep unlimited as unlimited
            await SetUserCredits(userId, newTotal, ddb, logger);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error granting credits: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Notify admin when credits are purchased (called from Stripe webhook or admin grant).
    /// </summary>
    public static async Task NotifyCreditPurchase(string userId, int creditsPurchased, decimal? amount, IAmazonDynamoDB ddb, IAmazonSimpleEmailService ses, string fromEmail, ILambdaLogger logger)
    {
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogWarning("SES_FROM_EMAIL not set; skipping credit purchase notification.");
            return;
        }

        try
        {
            var userCredits = await GetUserCredits(userId, ddb, logger);
            var subject = $"Credit Purchase: {creditsPurchased} credits";
            var body = $@"New credit purchase:

User ID: {userId}
Credits Purchased: {creditsPurchased}
New Balance: {userCredits}
Amount: {(amount.HasValue ? $"${amount.Value:F2}" : "N/A")}
Timestamp: {DateTimeOffset.UtcNow:O}
";

            await ses.SendEmailAsync(new Amazon.SimpleEmail.Model.SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Amazon.SimpleEmail.Model.Destination { ToAddresses = new List<string> { fromEmail } },
                Message = new Amazon.SimpleEmail.Model.Message
                {
                    Subject = new Amazon.SimpleEmail.Model.Content(subject),
                    Body = new Amazon.SimpleEmail.Model.Body { Text = new Amazon.SimpleEmail.Model.Content(body) }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to send credit purchase notification: {ex}");
        }
    }

    /// <summary>
    /// Generate userId from IP or use provided userId from header.
    /// </summary>
    public static string GetUserId(APIGatewayProxyRequest request, string? clientIp = null)
    {
        // Check for userId in headers (from frontend localStorage)
        if (request.Headers?.ContainsKey("x-user-id") == true)
        {
            var headerUserId = request.Headers["x-user-id"];
            if (!string.IsNullOrWhiteSpace(headerUserId))
                return headerUserId;
        }

        // Fallback to IP-based ID (anonymous user)
        var ip = clientIp ?? "unknown";
        return $"ip_{ip.Replace(".", "_").Replace(":", "_")}";
    }
}

