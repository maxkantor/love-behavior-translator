# Verify Webhook Setup - Step by Step

## ✅ What We Know Works
- `/stripe/webhook` endpoint exists in API Gateway
- POST method exists with Lambda integration
- OPTIONS method exists for CORS

## 🔍 What to Check Now

### Step 1: Verify POST Method Configuration

1. API Gateway → Your API → Resources → `/stripe/webhook`
2. Click on the **POST** method
3. Check **Integration Request**:
   - Integration type: Should be **Lambda Function**
   - ✅ **Use Lambda Proxy integration**: Should be **CHECKED**
   - Lambda Function: Should be `LoveBehaviorTranslatorFunction`
   - Lambda Region: Should be `us-east-1` (or your region)
4. If "Use Lambda Proxy integration" is NOT checked:
   - Click **Edit**
   - ✅ Check "Use Lambda Proxy integration"
   - Click **Save**

### Step 2: Verify API is Deployed

1. API Gateway → Your API → **Stages**
2. Click on your stage (e.g., `prod`)
3. Check **"Last updated"** timestamp - should be recent
4. If it's old or you see "Deploy API" button:
   - Click **Actions** → **Deploy API**
   - Select your stage
   - Click **Deploy**
   - Wait 10-30 seconds

### Step 3: Get Your Webhook URL

1. API Gateway → Your API → **Stages** → Your stage
2. Copy the **Invoke URL** at the top
   - Example: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod`
3. Your webhook URL should be: `{Invoke URL}/stripe/webhook`
   - Example: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`

### Step 4: Verify Stripe Webhook URL

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find your webhook for "love-behavior-translator"
3. Click on it
4. Check **Endpoint URL**:
   - Must match exactly: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
   - Must end with `/stripe/webhook`
   - Must use the correct stage name (`prod` vs `default`)
5. If it doesn't match:
   - Click **Edit**
   - Update **Endpoint URL** to match Step 3
   - Click **Save**

### Step 5: Check Webhook Events in Stripe

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Event deliveries"** tab
3. Look for `checkout.session.completed` events
4. Check the status:
   - **200 OK** (green) = Webhook succeeded, check CloudWatch logs
   - **400 ERR** (red) = Webhook failed, check error message
   - **404 ERR** (red) = Endpoint not found, check URL
   - **No events** = Webhook not receiving events

### Step 6: Check CloudWatch Logs

1. AWS Console → **CloudWatch** → **Log groups**
2. Find `/aws/lambda/LoveBehaviorTranslatorFunction`
3. Click on it → **Log streams** → Select the most recent stream
4. Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
5. Look for:
   - `🔔 Stripe webhook received!` - Webhook was called
   - `✅ Matched Stripe webhook endpoint!` - Endpoint matched
   - `Processing Stripe webhook event: checkout.session.completed` - Event processed
   - `Granting 20 credits to user...` - Credits being granted
   - `✅ Credits granted: 20 to user...` - Success!

### Step 7: Test Webhook Manually

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Send test webhook"**
3. Select event: `checkout.session.completed`
4. Click **"Send test webhook"**
5. **Immediately** check CloudWatch logs (refresh the log stream)
6. You should see webhook processing logs

### Step 8: Verify Lambda Function Has Latest Code

1. AWS Console → **Lambda** → `LoveBehaviorTranslatorFunction`
2. Check **"Last modified"** date
3. Should be recent (after we fixed the API version mismatch)
4. If it's old:
   - Upload `backend/dist/function.zip`
   - Wait for update to complete

## Common Issues

### Issue: Webhook shows "404 ERR" in Stripe

**Cause:** Webhook URL doesn't match API Gateway endpoint

**Fix:**
1. Get the correct Invoke URL from API Gateway Stages
2. Update Stripe webhook URL to: `{Invoke URL}/stripe/webhook`
3. Make sure stage name matches (`prod` vs `default`)

### Issue: Webhook shows "400 ERR" in Stripe

**Cause:** API version mismatch or Lambda error

**Fix:**
1. Upload latest `backend/dist/function.zip` to Lambda
2. Verify `STRIPE_WEBHOOK_SECRET` is set in Lambda environment variables
3. Check CloudWatch logs for specific error

### Issue: Webhook shows "200 OK" but credits not granted

**Cause:** Error in credit granting logic or userId mismatch

**Fix:**
1. Check CloudWatch logs for errors
2. Verify userId in webhook metadata matches frontend userId
3. Check DynamoDB table for the user

### Issue: No webhook events at all

**Cause:** Webhook not configured or wrong events selected

**Fix:**
1. Stripe Dashboard → Webhooks → Your webhook
2. Check "Events to send" includes `checkout.session.completed`
3. Verify webhook is "Active" (not disabled)

## Quick Test Checklist

Run through this checklist:

- [ ] POST method has "Use Lambda Proxy integration" checked
- [ ] API is deployed to your stage (check "Last updated" timestamp)
- [ ] Stripe webhook URL matches API Gateway Invoke URL + `/stripe/webhook`
- [ ] Webhook listens for `checkout.session.completed` events
- [ ] Lambda function has latest code (check "Last modified" date)
- [ ] `STRIPE_WEBHOOK_SECRET` is set in Lambda environment variables
- [ ] Test webhook shows "200 OK" in Stripe Dashboard
- [ ] CloudWatch logs show `🔔 Stripe webhook received!`

## If Still Not Working

Please share:
1. **Stripe webhook delivery status** (from Stripe Dashboard → Webhooks → Event deliveries)
2. **CloudWatch logs** (filter for "Stripe webhook" or "checkout.session.completed")
3. **API Gateway Invoke URL** (from Stages)
4. **Stripe webhook URL** (from Stripe Dashboard)

This will help identify the exact issue.

