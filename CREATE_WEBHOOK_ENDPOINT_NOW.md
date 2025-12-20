# URGENT: Create Webhook Endpoint Now (404 Error)

## The Problem
Stripe is trying to send webhook events but getting 404 errors. The `/stripe/webhook` endpoint doesn't exist in API Gateway.

## Quick Fix (5 minutes)

### Step 1: Create `/stripe` Resource (if it doesn't exist)

1. AWS Console → **API Gateway**
2. Select your API (`love-behavior-translator-api` or similar)
3. Click **Resources** in the left sidebar
4. Look for `/stripe` in the resource tree
   - ✅ **If it exists**: Go to Step 2
   - ❌ **If it doesn't exist**:
     - Click **Create resource** (at the root `/`)
     - **Resource name**: `stripe`
     - **Resource path**: `/stripe`
     - Click **Create resource**

### Step 2: Create `/stripe/webhook` Resource

1. With `/stripe` selected → Click **Create resource**
2. **Resource name**: `webhook`
3. **Resource path**: `/webhook`
   - API Gateway will show the full path as `/stripe/webhook`
4. Click **Create resource**

### Step 3: Create POST Method

1. Select `/stripe/webhook` resource
2. Click **Create method**
3. Select `POST` from the dropdown
4. Click the checkmark ✓
5. **Integration type**: Select **Lambda Function**
6. ✅ **Check "Use Lambda Proxy integration"** (CRITICAL!)
7. **Lambda Function**: Type `LoveBehaviorTranslatorFunction`
8. **Lambda Region**: Should auto-detect (e.g., `us-east-1`)
9. Click **Save**
10. If prompted: Click **OK** to grant API Gateway permission to invoke Lambda

### Step 4: Enable CORS

1. With `/stripe/webhook` selected → Click **Actions** → **Enable CORS**
2. **Access-Control-Allow-Origin**: `*`
3. **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id,stripe-signature`
   - ⚠️ **IMPORTANT**: Include `stripe-signature`!
4. **Access-Control-Allow-Methods**: `POST,OPTIONS`
5. Click **Enable CORS and replace existing CORS headers**
6. Click **Yes, replace existing values** when prompted

### Step 5: Deploy the API (CRITICAL!)

1. Click **Actions** → **Deploy API**
2. **Deployment stage**: Select your stage (usually `prod` or `default`)
3. **Deployment description**: "Add webhook endpoint"
4. Click **Deploy**
5. **Wait 10-30 seconds** for deployment to complete

### Step 6: Get Your Webhook URL

1. After deployment, go to **Stages** → Your stage (e.g., `prod`)
2. Copy the **Invoke URL** at the top
   - Example: `https://abc123.execute-api.us-east-1.amazonaws.com/prod`
3. Your webhook URL is: `{Invoke URL}/stripe/webhook`
   - Example: `https://abc123.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`

### Step 7: Update Stripe Webhook URL

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find your webhook (or create a new one)
3. Click **Edit** (or click on it)
4. **Endpoint URL**: Paste the URL from Step 6
   - Must be: `https://{your-api-gateway-id}.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
   - Must end with `/stripe/webhook`
5. **Events to send**: Make sure `checkout.session.completed` is selected
6. Click **Save**

### Step 8: Test the Webhook

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Send test webhook"**
3. Select event: `checkout.session.completed`
4. Click **"Send test webhook"**
5. Check CloudWatch logs immediately:
   - CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - You should see: `🔔 Stripe webhook received!`
   - And: `Processing Stripe webhook event: checkout.session.completed`
   - And: `✅ Credits granted: 20 to user...`

## Verification Checklist

After completing the steps above:

- [ ] `/stripe` resource exists in API Gateway
- [ ] `/stripe/webhook` resource exists
- [ ] `POST` method exists on `/stripe/webhook` with Lambda integration
- [ ] CORS is enabled on `/stripe/webhook`
- [ ] API is deployed to your stage
- [ ] Stripe webhook URL matches your API Gateway endpoint + `/stripe/webhook`
- [ ] Test webhook shows "200 OK" in Stripe Dashboard
- [ ] CloudWatch logs show `🔔 Stripe webhook received!`

## If You Still Get 404

1. **Double-check the webhook URL in Stripe:**
   - Must match exactly: `https://{api-gateway-id}.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
   - Check the stage name (`prod` vs `default`)

2. **Verify API is deployed:**
   - API Gateway → Your API → Stages → Your stage
   - Check "Last updated" timestamp is recent

3. **Check the resource path:**
   - API Gateway → Resources
   - Verify `/stripe/webhook` exists (not `/stripe/webhooks` or `/webhook`)

4. **Check CloudWatch logs:**
   - Look for requests to `/stripe/webhook`
   - If you see them but get 404, the endpoint isn't deployed
   - If you don't see them, Stripe isn't calling the endpoint (check URL)

## Once It Works

After the webhook is working:
- Credits will be granted automatically when payments complete
- You'll receive admin email notifications
- CloudWatch logs will show successful webhook processing

