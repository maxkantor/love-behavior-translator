# Quick Fix: Payment Notifications Not Working

## Most Common Issues (Check These First)

### 1. SES_FROM_EMAIL Not Set ⚠️

**Check:**
1. Lambda → **LoveBehaviorTranslatorFunction** → **Configuration** → **Environment variables**
2. Look for `SES_FROM_EMAIL`
3. If missing, add it:
   - **Key**: `SES_FROM_EMAIL`
   - **Value**: Your verified email (e.g., `support@lovebehaviortranslator.com`)
   - Click **Save**

**Verify in CloudWatch:**
- Look for log: `"SES_FROM_EMAIL not set; skipping credit purchase notification."`
- If you see this, the environment variable is missing

### 2. Email Not Verified in SES ⚠️

**Check:**
1. SES → **Verified identities**
2. Find your email address
3. Status should be **"Verified"** (green checkmark)

**If not verified:**
1. Click **Create identity** → **Email address**
2. Enter your email
3. Check inbox for verification link
4. Click the link

### 3. Check CloudWatch Logs (Most Important!)

**Steps:**
1. CloudWatch → **Log groups** → `/aws/lambda/LoveBehaviorTranslatorFunction`
2. Click the **most recent log stream**
3. Search for: `"Admin notification"` or `"NotifyCreditPurchase"`

**What You Should See:**

✅ **Success:**
```
✅ Admin notification sent for credit purchase: 20 credits by user_xxx
✅ Payment notification email sent to support@lovebehaviortranslator.com
```

❌ **Error - Missing Environment Variable:**
```
SES_FROM_EMAIL not set; skipping credit purchase notification.
```
→ **Fix**: Set `SES_FROM_EMAIL` in Lambda (see #1 above)

❌ **Error - Email Not Verified:**
```
Failed to send credit purchase notification: Email address not verified
```
→ **Fix**: Verify email in SES (see #2 above)

❌ **Error - Other SES Error:**
```
Failed to send credit purchase notification: [error message]
```
→ Share the error message for further help

### 4. Check if Webhook is Processing

**In CloudWatch logs, search for:**
- `"checkout.session.completed"`
- `"Granting"` or `"Credits granted"`

**You should see:**
```
Processing Stripe webhook event: checkout.session.completed
Granting 20 credits to user user_xxx from Stripe payment cs_test_xxx
Credits granted successfully. New balance for user_xxx: 25
✅ Admin notification sent for credit purchase: 20 credits by user_xxx
```

**If you DON'T see the notification log:**
- The notification function might not be called
- Check if there's an exception being caught silently

### 5. Test Email Sending Directly

**Test if SES works at all:**
1. SES → **Verified identities** → Select your email
2. Click **Send test email**
3. **From**: Your verified email
4. **To**: Your verified email
5. **Subject**: Test
6. **Body**: Test
7. Click **Send test email**
8. Check your inbox

**If this fails:**
- SES is not configured correctly
- Check SES account status

### 6. Check Spam Folder

- Check spam/junk folder
- Add sender to contacts

## Quick Action Plan

1. ✅ **Check CloudWatch logs** (most important - tells you exactly what's wrong)
2. ✅ **Verify SES_FROM_EMAIL** is set in Lambda
3. ✅ **Verify email** is verified in SES
4. ✅ **Test SES** by sending a test email
5. ✅ **Check spam folder**

## Share These Details for Help

If still not working, share:
1. **CloudWatch log snippet** (the part showing the notification attempt)
2. **SES_FROM_EMAIL value** (from Lambda environment variables)
3. **SES verification status** (from SES console)
4. **Any error messages** from CloudWatch logs

