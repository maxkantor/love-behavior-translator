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
    public static async Task NotifyCreditPurchase(string userId, int creditsPurchased, decimal? amount, IAmazonDynamoDB ddb, IAmazonSimpleEmailService ses, string fromEmail, ILambdaLogger logger, string? customerEmail = null, string? paymentId = null, string? sessionId = null, string? adminEmail = null, string? customerName = null, string? cardLast4 = null, DateTimeOffset? purchaseDate = null)
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
            
            var purchaseDateTime = purchaseDate ?? timestamp;
            var body = $@"🎉 NEW CREDIT PURCHASE

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
PAYMENT DETAILS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Amount Paid: ${(amount ?? 0):F2}
Credits Purchased: {creditsPurchased}
New User Balance: {userCredits}
{(string.IsNullOrWhiteSpace(cardLast4) ? "" : $"Card: •••• {cardLast4}\n")}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
USER INFORMATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

User ID: {userId}
{(string.IsNullOrWhiteSpace(customerName) ? "" : $"Name: {customerName}\n")}
{(string.IsNullOrWhiteSpace(customerEmail) ? "" : $"Email: {customerEmail}\n")}
Purchase Date: {purchaseDateTime:yyyy-MM-dd HH:mm:ss} UTC
{(string.IsNullOrWhiteSpace(paymentId) ? "" : $"Payment ID: {paymentId}\n")}
{(string.IsNullOrWhiteSpace(sessionId) ? "" : $"Checkout Session: {sessionId}\n")}

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

    private const string EmailVerificationTableName = "LoveBehaviorTranslatorEmailVerification";
    private const string EmailLinksTableName = "LoveBehaviorTranslatorEmailLinks";

    /// <summary>
    /// Generate a 6-digit verification code and store it with email.
    /// </summary>
    public static async Task<string> GenerateVerificationCode(string email, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        var code = new Random().Next(100000, 999999).ToString(); // 6-digit code
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds(); // 15 minute expiry

        try
        {
            await ddb.PutItemAsync(new PutItemRequest
            {
                TableName = EmailVerificationTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = email.ToLowerInvariant().Trim() },
                    ["code"] = new AttributeValue { S = code },
                    ["createdAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
                    ["ttl"] = new AttributeValue { N = expiresAt.ToString() }
                }
            });

            logger.LogInformation($"Generated verification code for {email}");
            return code;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error generating verification code: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Verify the code for an email and link it to a visitor ID.
    /// </summary>
    public static async Task<bool> VerifyCodeAndLinkEmail(string email, string code, string visitorId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var emailLower = email.ToLowerInvariant().Trim();
            
            // Get verification code
            var resp = await ddb.GetItemAsync(new GetItemRequest
            {
                TableName = EmailVerificationTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = emailLower }
                }
            });

            if (resp.Item.Count == 0 || !resp.Item.ContainsKey("code"))
            {
                logger.LogWarning($"No verification code found for {email}");
                return false;
            }

            var storedCode = resp.Item["code"].S;
            if (storedCode != code)
            {
                logger.LogWarning($"Invalid verification code for {email}");
                return false;
            }

            // Code is valid - link email to visitor ID
            await LinkEmailToVisitorId(emailLower, visitorId, ddb, logger);

            // Delete used verification code
            await ddb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = EmailVerificationTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = emailLower }
                }
            });

            logger.LogInformation($"Email {emailLower} verified and linked to visitor {visitorId}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error verifying code: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Link an email to a visitor ID (add visitor ID to email's list).
    /// </summary>
    public static async Task LinkEmailToVisitorId(string email, string visitorId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var emailLower = email.ToLowerInvariant().Trim();
            
            // Get existing visitor IDs for this email
            var resp = await ddb.GetItemAsync(new GetItemRequest
            {
                TableName = EmailLinksTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = emailLower }
                }
            });

            var visitorIds = new HashSet<string> { visitorId };
            
            if (resp.Item.Count > 0 && resp.Item.ContainsKey("visitorIds"))
            {
                var existingIds = resp.Item["visitorIds"].SS ?? new List<string>();
                foreach (var id in existingIds)
                {
                    visitorIds.Add(id);
                }
            }

            // Update with merged list
            await ddb.PutItemAsync(new PutItemRequest
            {
                TableName = EmailLinksTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = emailLower },
                    ["visitorIds"] = new AttributeValue { SS = visitorIds.ToList() },
                    ["updatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                }
            });

            logger.LogInformation($"Linked email {emailLower} to visitor {visitorId}. Total linked visitors: {visitorIds.Count}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error linking email to visitor ID: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get all visitor IDs linked to an email.
    /// </summary>
    public static async Task<List<string>> GetLinkedVisitorIds(string email, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var emailLower = email.ToLowerInvariant().Trim();
            
            var resp = await ddb.GetItemAsync(new GetItemRequest
            {
                TableName = EmailLinksTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["email"] = new AttributeValue { S = emailLower }
                }
            });

            if (resp.Item.Count == 0 || !resp.Item.ContainsKey("visitorIds"))
            {
                return new List<string>();
            }

            return resp.Item["visitorIds"].SS ?? new List<string>();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting linked visitor IDs: {ex}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Merge credits from all visitor IDs linked to an email and return total.
    /// </summary>
    public static async Task<int> MergeCreditsFromEmail(string email, string currentVisitorId, IAmazonDynamoDB ddb, ILambdaLogger logger)
    {
        try
        {
            var linkedVisitorIds = await GetLinkedVisitorIds(email, ddb, logger);
            
            // Always include current visitor ID
            if (!linkedVisitorIds.Contains(currentVisitorId))
            {
                linkedVisitorIds.Add(currentVisitorId);
            }

            if (linkedVisitorIds.Count == 0)
            {
                return await GetUserCredits(currentVisitorId, ddb, logger);
            }

            // Sum credits from all linked visitor IDs
            int totalCredits = 0;
            foreach (var visitorId in linkedVisitorIds)
            {
                var credits = await GetUserCredits(visitorId, ddb, logger);
                if (credits == -1)
                {
                    // Admin/unlimited - return unlimited
                    return -1;
                }
                totalCredits += credits;
            }

            // Set merged credits to current visitor ID
            await SetUserCredits(currentVisitorId, totalCredits, ddb, logger);

            // Optionally zero out other visitor IDs (or leave them for audit)
            // For now, we'll leave them but set current visitor to merged total

            logger.LogInformation($"Merged credits from {linkedVisitorIds.Count} visitor IDs for email {email}. Total: {totalCredits}");
            return totalCredits;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error merging credits from email: {ex}");
            // Fallback to current visitor's credits
            return await GetUserCredits(currentVisitorId, ddb, logger);
        }
    }

    /// <summary>
    /// Send verification code email.
    /// </summary>
    public static async Task SendVerificationEmail(string email, string code, IAmazonSimpleEmailService ses, string fromEmail, ILambdaLogger logger)
    {
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogWarning("SES_FROM_EMAIL not set; cannot send verification email.");
            throw new Exception("Email service not configured");
        }

        var subject = "Your Love Behavior Translator Verification Code";
        var body = $@"Hello,

Please use this code to verify your email and restore your credits:

{code}

This code will expire in 15 minutes.

If you didn't request this code, you can safely ignore this email.

Best regards,
Love Behavior Translator
";

        try
        {
            await ses.SendEmailAsync(new SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Destination { ToAddresses = new List<string> { email } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            });

            logger.LogInformation($"Verification email sent to {email}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to send verification email: {ex}");
            throw;
        }
    }
}

