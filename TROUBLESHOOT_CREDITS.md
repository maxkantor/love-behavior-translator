# Troubleshooting: Credits Not Being Updated After Payment

## Quick Checklist

1. ✅ **Did you complete the payment?**
   - Check Stripe Dashboard → Payments
   - Find your payment → Check `payment_status` should be `"paid"`
   - Check `status` should be `"complete"`

2. ✅ **Is the webhook configured?**
   - Stripe Dashboard → Webhooks
   - Find your webhook endpoint
   - Check it's "Active" (green badge)
   - Check it listens for `checkout.session.completed` events

3. ✅ **Is the webhook endpoint working?**
   - Check Stripe Dashboard → Webhooks → Your webhook → "Event deliveries"
   - Look for `checkout.session.completed` events
   - Status should be "200 OK" (green) not "400 ERR" or "Failed"

4. ✅ **Is Lambda function updated?**
   - AWS Console → Lambda → `LoveBehaviorTranslatorFunction`
   - Check "Last modified" date
   - Should be recent (after we fixed the API version mismatch)

5. ✅ **Check CloudWatch logs:**
   - CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
   - Look for:
     - `🔔 Stripe webhook received!` - Webhook was called
     - `Processing Stripe webhook event: checkout.session.completed` - Event was parsed
     - `Granting X credits to user...` - Credits being granted
     - `✅ Credits granted: X to user...` - Success message

## Step-by-Step Debugging

### Step 1: Verify Payment Was Completed

1. Stripe Dashboard → **Payments**
2. Find the payment you made
3. Click on it
4. Check:
   - **Status**: Should be "Succeeded" (green)
   - **Payment status**: Should be "Paid"
   - If it's "Pending" or "Failed", the payment didn't complete

### Step 2: Check Webhook Delivery

1. Stripe Dashboard → **Webhooks**
2. Find your webhook (should show your API Gateway URL)
3. Click on it
4. Go to **"Event deliveries"** tab
5. Look for `checkout.session.completed` events
6. Check the status:
   - **200 OK** (green) = Webhook succeeded, check CloudWatch logs
   - **400 ERR** (red) = Webhook failed, check error message
   - **No events** = Webhook not configured or not receiving events

### Step 3: Check CloudWatch Logs

1. AWS Console → **CloudWatch** → **Log groups**
2. Find `/aws/lambda/LoveBehaviorTranslatorFunction`
3. Click on it → **Log streams** → Select the most recent stream
4. Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
5. Look for these messages in order:
   - `🔔 Stripe webhook received!` - Webhook endpoint was called
   - `Processing Stripe webhook event: checkout.session.completed` - Event type matched
   - `Session metadata: userId=user_..., credits=20, price=4.99` - Metadata found
   - `Granting 20 credits to user user_...` - Credits being granted
   - `Credits granted successfully. New balance for user_...: X` - Success!

### Step 4: Verify Lambda Function Has Latest Code

1. AWS Console → **Lambda** → `LoveBehaviorTranslatorFunction`
2. Check **"Last modified"** date
3. If it's old, upload the new `backend/dist/function.zip`:
   - Click **"Upload from"** → **".zip file"**
   - Select `backend/dist/function.zip`
   - Click **Save**

### Step 5: Test Webhook Manually

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Send test webhook"**
3. Select event: `checkout.session.completed`
4. Click **"Send test webhook"**
5. Immediately check CloudWatch logs
6. You should see the webhook being processed

## Common Issues and Fixes

### Issue: Webhook shows "400 ERR" in Stripe

**Cause:** API version mismatch or webhook endpoint error

**Fix:**
1. Make sure Lambda function has the latest code (with `throwOnApiVersionMismatch: false`)
2. Upload `backend/dist/function.zip` to Lambda
3. Test webhook again

### Issue: Webhook shows "Failed" or "No response"

**Cause:** Webhook endpoint doesn't exist or API Gateway not deployed

**Fix:**
1. Verify `/stripe/webhook` endpoint exists in API Gateway
2. Make sure API is deployed (Actions → Deploy API)
3. Verify webhook URL in Stripe matches your API Gateway endpoint

### Issue: Webhook succeeds (200 OK) but credits not granted

**Cause:** userId mismatch or error in credit granting

**Fix:**
1. Check CloudWatch logs for errors
2. Verify userId in webhook metadata matches frontend userId
3. Check DynamoDB table `LoveBehaviorTranslatorUsers` for the user

### Issue: No webhook events at all

**Cause:** Webhook not configured or wrong events selected

**Fix:**
1. Stripe Dashboard → Webhooks → Your webhook
2. Check "Events to send" includes `checkout.session.completed`
3. Verify webhook is "Active" (not disabled)

## Manual Credit Grant (Temporary Fix)

If webhook is not working and you need to grant credits immediately:

1. Go to Admin Dashboard: `https://lovebehaviortranslator.com/admin`
2. Login with admin password
3. Find the user (by userId: `user_1766068113029_pwij2ghjs`)
4. Click "Grant Credits"
5. Enter the number of credits (e.g., 20)
6. Click "Grant"

This is a temporary workaround while we fix the webhook.

