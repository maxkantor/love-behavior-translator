# Upload Lambda Function to Fix API Version Error

## The Problem

You're getting 400 errors with this message:
```
"Invalid webhook: Received event with API version 2025-09-30.clover, but Stripe.net 45.0.0 expects API version 2024-06-20"
```

## The Fix

The code has been fixed to handle this, but you need to upload the new Lambda function.

### Step 1: Upload the New Lambda Function

1. AWS Console → **Lambda**
2. Find and select `LoveBehaviorTranslatorFunction`
3. Scroll down to **"Code source"** section
4. Click **"Upload from"** → **".zip file"**
5. Select `backend/dist/function.zip` from your local machine
6. Click **Save**
7. Wait for the upload to complete (usually 10-30 seconds)

### Step 2: Verify the Fix is Applied

1. After upload, check **"Last modified"** date - should be recent
2. The function should now handle API version mismatch

### Step 3: Test the Webhook

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Send test webhook"**
3. Select event: `checkout.session.completed`
4. Click **"Send test webhook"**
5. Check the status - should now be **200 OK** (green) instead of **400 ERR** (red)

### Step 4: Check CloudWatch Logs

1. CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
2. Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
3. You should see:
   - `🔔 Stripe webhook received!`
   - `Processing Stripe webhook event: checkout.session.completed`
   - `Granting 20 credits to user...`
   - `✅ Credits granted: 20 to user...`

### Step 5: Make a Real Purchase to Test

1. Go to your website
2. Make a test purchase
3. Check Stripe Dashboard → **Webhooks** → **Event deliveries**
4. The `checkout.session.completed` event should show **200 OK**
5. Check your credits - they should be updated!

## Important Notes

- Make sure you're uploading to the **correct Lambda function**: `LoveBehaviorTranslatorFunction`
- The zip file is located at: `backend/dist/function.zip`
- After uploading, wait a few seconds for the function to update
- Test with a real purchase to verify credits are granted

## If Still Getting 400 Errors

1. Verify the Lambda function was uploaded successfully
2. Check CloudWatch logs for the exact error message
3. Make sure `STRIPE_WEBHOOK_SECRET` is set in Lambda environment variables
4. Verify the webhook URL in Stripe matches your API Gateway endpoint

