# Debug Payment Notifications

## Quick Checklist

1. ✅ **Lambda function uploaded** with latest code
2. ✅ **SES_FROM_EMAIL** set in Lambda environment variables
3. ✅ **Email verified** in SES
4. ✅ **Webhook processing** successfully (check CloudWatch logs)
5. ✅ **Notification function called** (check CloudWatch logs)

## Step 1: Check CloudWatch Logs

1. AWS Console → **CloudWatch** → **Log groups**
2. Find: `/aws/lambda/LoveBehaviorTranslatorFunction`
3. Click on the most recent log stream
4. Search for: `"Admin notification"` or `"NotifyCreditPurchase"`

### What to Look For

**✅ Success:**
```
✅ Admin notification sent for credit purchase: 20 credits by user_xxx
✅ Payment notification email sent to support@lovebehaviortranslator.com
```

**❌ Errors:**
```
⚠️ Failed to send admin notification (credits still granted): ...
SES_FROM_EMAIL not set; skipping credit purchase notification.
Failed to send credit purchase notification: ...
```

## Step 2: Verify Lambda Environment Variables

1. Lambda → **LoveBehaviorTranslatorFunction** → **Configuration** → **Environment variables**
2. Check for `SES_FROM_EMAIL`:
   - ✅ **Present**: Value should be your verified email (e.g., `support@lovebehaviortranslator.com`)
   - ❌ **Missing**: Add it now (see Step 3)

## Step 3: Set SES_FROM_EMAIL (If Missing)

1. Lambda → **LoveBehaviorTranslatorFunction** → **Configuration** → **Environment variables**
2. Click **Edit**
3. Click **Add environment variable**
4. **Key**: `SES_FROM_EMAIL`
5. **Value**: Your verified SES email (e.g., `support@lovebehaviortranslator.com`)
6. Click **Save**

> **Important**: After adding/updating environment variables, you don't need to redeploy the Lambda function - it will pick up the new value immediately.

## Step 4: Verify Email in SES

1. AWS Console → **SES** → **Verified identities**
2. Find your email address
3. Check status:
   - ✅ **Verified**: Green checkmark
   - ❌ **Pending**: Check your email for verification link
   - ❌ **Not found**: Create and verify it (see below)

### If Email Not Verified

1. SES → **Verified identities** → **Create identity**
2. **Identity type**: **Email address**
3. **Email address**: Enter your admin email
4. Click **Create identity**
5. Check your email inbox for verification link
6. Click the link to verify

## Step 5: Check Webhook Processing

Make sure the webhook is actually processing payments:

1. CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
2. Search for: `"checkout.session.completed"` or `"Granting"` or `"Credits granted"`
3. You should see:
   ```
   Processing Stripe webhook event: checkout.session.completed
   Granting 20 credits to user user_xxx from Stripe payment cs_test_xxx
   Credits granted successfully. New balance for user_xxx: 25
   ```

If you don't see these logs, the webhook might not be processing correctly.

## Step 6: Test Email Sending Directly

Test if SES can send emails at all:

1. SES → **Verified identities** → Select your email
2. Click **Send test email**
3. **From**: Your verified email
4. **To**: Your verified email (same or different)
5. **Subject**: Test
6. **Body**: Test message
7. Click **Send test email**
8. Check your inbox

If this fails, there's an SES configuration issue.

## Step 7: Check Spam Folder

- Check your spam/junk folder
- Add `SES_FROM_EMAIL` to your contacts to prevent future spam filtering

## Step 8: Verify Lambda Function Code

Make sure you uploaded the latest Lambda function:

1. Check the zip file timestamp:
   ```
   C:\Apps\LoveBehaviorTranslator\backend\dist\function.zip
   ```
2. Should be recent (within the last few minutes if you just built it)
3. If not, rebuild:
   ```powershell
   cd backend
   .\build.ps1
   ```
4. Upload to Lambda again

## Common Issues

### Issue 1: "SES_FROM_EMAIL not set"

**Symptom**: CloudWatch logs show:
```
SES_FROM_EMAIL not set; skipping credit purchase notification.
```

**Fix**: Set `SES_FROM_EMAIL` in Lambda environment variables (Step 3 above)

### Issue 2: "Email address not verified"

**Symptom**: CloudWatch logs show:
```
Failed to send credit purchase notification: Email address not verified
```

**Fix**: Verify your email in SES (Step 4 above)

### Issue 3: "SES sandbox mode"

**Symptom**: Can only send to verified emails (this is fine for admin notifications)

**Fix**: If you want to send to any email, request production access:
1. SES → **Account dashboard**
2. Click **Request production access**
3. Fill out the form
4. Wait for approval (24-48 hours)

### Issue 4: Credits granted but no email

**Symptom**: Credits are added successfully, but no email notification

**Possible causes**:
1. `SES_FROM_EMAIL` not set
2. Email not verified in SES
3. Email in spam folder
4. SES sending failed (check CloudWatch logs for error)

**Fix**: Follow Steps 1-7 above

## Still Not Working?

If you've checked everything above and still not receiving emails:

1. **Share CloudWatch logs**: Copy the relevant log entries (especially any errors)
2. **Check SES sending statistics**:
   - SES → **Account dashboard** → **Sending statistics**
   - Look for bounces, complaints, or delivery issues
3. **Check SES sending quota**:
   - SES → **Account dashboard**
   - Verify you haven't exceeded your sending quota

