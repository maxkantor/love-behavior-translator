# Payment Notifications Setup

## Overview

You will receive an email notification every time a customer completes a payment through Stripe. The notification includes:

- Payment amount
- Number of credits purchased
- User ID
- Customer email (if provided)
- Payment ID
- Checkout session ID
- New user credit balance
- Timestamp

## Prerequisites

1. **SES Email Verified**: The email address you want to receive notifications at must be verified in AWS SES
2. **Lambda Environment Variable**: `SES_FROM_EMAIL` must be set in your Lambda function
3. **SES Production Access**: If you're in SES sandbox mode, you can only send to verified emails (which is fine for admin notifications)

## Setup Steps

### Step 1: Verify Your Admin Email in SES

1. AWS Console → **SES** → **Verified identities**
2. Click **Create identity**
3. **Identity type**: **Email address**
4. **Email address**: Enter your admin email (e.g., `your-email@gmail.com`)
5. Click **Create identity**
6. Check your email and click the verification link

### Step 2: Set SES_FROM_EMAIL in Lambda

1. Lambda → **LoveBehaviorTranslatorFunction** → **Configuration** → **Environment variables**
2. Find or add: `SES_FROM_EMAIL`
3. **Value**: Your verified email address (e.g., `support@lovebehaviortranslator.com` or `your-email@gmail.com`)
4. Click **Save**

> **Note**: The notification will be sent FROM and TO this email address (you'll receive it at this address).

### Step 3: Upload Updated Lambda Function

The notification feature is already in the code, but make sure you have the latest version:

1. Build the Lambda function (if you haven't already):
   ```powershell
   cd backend
   .\build.ps1
   ```

2. Upload to Lambda:
   - Lambda → **LoveBehaviorTranslatorFunction** → **Code source**
   - Click **Upload from** → **.zip file**
   - Select: `C:\Apps\LoveBehaviorTranslator\backend\dist\function.zip`
   - Click **Save**

## Testing

### Test with a Real Payment

1. Go to your website → Click **Unlock Clarity**
2. Select a credit pack (e.g., Starter Pack - 20 credits for $4.99)
3. Complete the Stripe checkout with a test card:
   - Card: `4242 4242 4242 4242`
   - Expiry: Any future date
   - CVC: Any 3 digits
   - ZIP: Any 5 digits
4. Complete the payment
5. Check your email inbox (and spam folder) for the notification

### Test with Stripe Dashboard

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find your webhook endpoint
3. Click **Send test webhook**
4. Select event: `checkout.session.completed`
5. Click **Send test webhook**
6. Check your email for the notification

## Notification Email Format

You'll receive an email like this:

```
Subject: 💰 New Payment: 20 credits - $4.99

🎉 NEW CREDIT PURCHASE

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
PAYMENT DETAILS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Amount Paid: $4.99
Credits Purchased: 20
New User Balance: 25

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
USER INFORMATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

User ID: user_1766068113029_pwij2ghjs
Customer Email: customer@example.com
Payment ID: pi_xxxxx
Checkout Session: cs_test_xxxxx

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIMESTAMP
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

2025-12-20 18:30:45 UTC
(2025-12-20T18:30:45.000Z)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

View in Stripe Dashboard: https://dashboard.stripe.com/payments
```

## Troubleshooting

### Not Receiving Emails

1. **Check SES_FROM_EMAIL is set**:
   - Lambda → Configuration → Environment variables
   - Verify `SES_FROM_EMAIL` exists and has your verified email

2. **Check CloudWatch Logs**:
   - CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - Filter for: `"Admin notification"`
   - Look for:
     - `✅ Admin notification sent for credit purchase: ...`
     - `⚠️ Failed to send admin notification: ...`

3. **Check SES Email Verification**:
   - SES → Verified identities
   - Ensure your admin email is verified and shows "Verified" status

4. **Check Spam Folder**:
   - The notification email might be in your spam/junk folder
   - Add `SES_FROM_EMAIL` to your contacts to prevent this

5. **Check SES Sandbox Mode**:
   - If you're in SES sandbox mode, you can only send to verified emails
   - This is fine for admin notifications (you only need to verify your own email)
   - To send to any email, request production access in SES

### Email Sending Errors

If CloudWatch logs show errors like:
- `"Email address not verified"`
- `"SES_FROM_EMAIL not set"`

**Fix**:
1. Verify the email in SES (Step 1 above)
2. Set `SES_FROM_EMAIL` in Lambda (Step 2 above)
3. Rebuild and upload Lambda function

### Credits Granted but No Email

If credits are granted successfully but no email is sent:

1. Check CloudWatch logs for the exact error message
2. Verify `SES_FROM_EMAIL` is set correctly
3. Check that the email is verified in SES
4. Try sending a test email from SES console to verify SES is working

## Production Setup

When going live:

1. **Request SES Production Access** (if you want to send to any email):
   - SES → Account dashboard → Request production access
   - Fill out the form and wait for approval (usually 24-48 hours)

2. **Use a Professional Email**:
   - Set `SES_FROM_EMAIL` to a professional address like `support@lovebehaviortranslator.com`
   - Verify this domain in SES (not just the email)

3. **Set Up Email Filtering** (Optional):
   - Create an email filter/rule to automatically organize payment notifications
   - Example: Label emails with subject containing "New Payment"

## Viewing Payment History

You can also view all payments in:
- **Stripe Dashboard**: https://dashboard.stripe.com/payments
- **Admin Dashboard**: Your app's admin panel (if you've implemented it)

