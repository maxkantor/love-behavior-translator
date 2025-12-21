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
    public static async Task NotifyCreditPurchase(string userId, int creditsPurchased, decimal? amount, IAmazonDynamoDB ddb, IAmazonSimpleEmailService ses, string fromEmail, ILambdaLogger logger, string? customerEmail = null, string? paymentId = null, string? sessionId = null, string? adminEmail = null)
    {
        logger.LogInformation($"📧 NotifyCreditPurchase called: userId={userId}, credits={creditsPurchased}, amount={amount}, fromEmail='{fromEmail}'");
        
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogWarning("❌ SES_FROM_EMAIL not set; skipping credit purchase notification.");
            return;
        }

        try
        {
            logger.LogInformation($"📧 Preparing email notification to {fromEmail}");
            var userCredits = await GetUserCredits(userId, ddb, logger);
            var timestamp = DateTimeOffset.UtcNow;
            var subject = $"💰 New Payment: {creditsPurchased} credits - ${(amount ?? 0):F2}";
            
            var body = $@"🎉 NEW CREDIT PURCHASE

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
PAYMENT DETAILS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Amount Paid: ${(amount ?? 0):F2}
Credits Purchased: {creditsPurchased}
New User Balance: {userCredits}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
USER INFORMATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

User ID: {userId}
{(string.IsNullOrWhiteSpace(customerEmail) ? "" : $"Customer Email: {customerEmail}\n")}
{(string.IsNullOrWhiteSpace(paymentId) ? "" : $"Payment ID: {paymentId}\n")}
{(string.IsNullOrWhiteSpace(sessionId) ? "" : $"Checkout Session: {sessionId}\n")}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIMESTAMP
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{timestamp:yyyy-MM-dd HH:mm:ss} UTC
({timestamp:O})

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

View in Stripe Dashboard: https://dashboard.stripe.com/payments
";

            var adminEmailToUse = string.IsNullOrWhiteSpace(adminEmail) ? fromEmail : adminEmail;
            logger.LogInformation($"📧 Calling SES.SendEmailAsync: From={fromEmail}, To={adminEmailToUse}, Subject={subject}");
            
            var request = new Amazon.SimpleEmail.Model.SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Amazon.SimpleEmail.Model.Destination { ToAddresses = new List<string> { adminEmailToUse } },
                Message = new Amazon.SimpleEmail.Model.Message
                {
                    Subject = new Amazon.SimpleEmail.Model.Content(subject),
                    Body = new Amazon.SimpleEmail.Model.Body { Text = new Amazon.SimpleEmail.Model.Content(body) }
                }
            };
            
            var response = await ses.SendEmailAsync(request);
            
            logger.LogInformation($"✅ SES.SendEmailAsync succeeded! MessageId={response.MessageId}, HttpStatusCode={response.HttpStatusCode}");
            logger.LogInformation($"✅ Payment notification email sent to {adminEmailToUse} (MessageId: {response.MessageId})");
        }
        catch (Amazon.SimpleEmail.Model.MessageRejectedException ex)
        {
            logger.LogError($"❌ SES MessageRejectedException: {ex.Message}");
            logger.LogError($"❌ Error Code: {ex.ErrorCode}, Status Code: {ex.StatusCode}");
            if (ex.InnerException != null)
            {
                logger.LogError($"❌ Inner Exception: {ex.InnerException}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Failed to send credit purchase notification: {ex.GetType().Name}: {ex.Message}");
            logger.LogError($"❌ Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                logger.LogError($"❌ Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
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

