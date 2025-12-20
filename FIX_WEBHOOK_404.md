# Fix: Webhook 404 Error

## Problem
Stripe is trying to call your webhook but getting a 404 error. This means the `/stripe/webhook` endpoint doesn't exist in API Gateway or isn't deployed.

## Solution: Create the Webhook Endpoint in API Gateway

### Step 1: Verify `/stripe` Resource Exists

1. AWS Console → **API Gateway**
2. Select your API (`love-behavior-translator-api` or similar)
3. Click **Resources** in the left sidebar
4. Look for `/stripe` in the resource tree
   - If it exists, go to Step 2
   - If it doesn't exist, create it:
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
6. ✅ **Check "Use Lambda Proxy integration"** (IMPORTANT!)
7. **Lambda Function**: Type or select `LoveBehaviorTranslatorFunction`
8. **Lambda Region**: Should auto-detect (e.g., `us-east-1`)
9. Click **Save**
10. If prompted: Click **OK** to grant API Gateway permission to invoke Lambda

### Step 4: Enable CORS (Important!)

1. With `/stripe/webhook` selected → Click **Actions** → **Enable CORS**
2. **Access-Control-Allow-Origin**: `*`
3. **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id,stripe-signature`
   - ⚠️ Make sure `stripe-signature` is included!
4. **Access-Control-Allow-Methods**: `POST,OPTIONS`
5. Click **Enable CORS and replace existing CORS headers**

### Step 5: Create OPTIONS Method (For CORS Preflight)

1. With `/stripe/webhook` selected → Click **Create method**
2. Select `OPTIONS` from the dropdown
3. Click the checkmark ✓
4. **Integration type**: Select **Mock**
5. Click **Save**
6. Click on the `OPTIONS` method → **Integration Response**
7. Expand **Method Response** → **200**
8. Add headers:
   - `Access-Control-Allow-Origin`
   - `Access-Control-Allow-Headers`
   - `Access-Control-Allow-Methods`
9. Go to **Integration Response** → Expand **200**
10. Set header mappings:
    - `Access-Control-Allow-Origin`: `'*'`
    - `Access-Control-Allow-Headers`: `'Content-Type,Authorization,x-user-id,stripe-signature'`
    - `Access-Control-Allow-Methods`: `'POST,OPTIONS'`

**OR** use the simpler approach:
- After Step 4 (Enable CORS), API Gateway should have automatically created the OPTIONS method
- If it did, you're done with this step!

### Step 6: Deploy the API (CRITICAL!)

1. Click **Actions** → **Deploy API**
2. **Deployment stage**: Select your stage (usually `prod` or `default`)
3. **Deployment description**: "Add webhook endpoint"
4. Click **Deploy**
5. **Wait 10-30 seconds** for deployment to complete

### Step 7: Verify the Endpoint

1. After deployment, go to **Stages** → Your stage (e.g., `prod`)
2. You should see the **Invoke URL** at the top
3. Your webhook URL should be: `{Invoke URL}/stripe/webhook`
   - Example: `https://abc123.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
4. Copy this URL

### Step 8: Update Stripe Webhook URL

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find your webhook endpoint
3. Click **Edit** (or click on it)
4. Update **Endpoint URL** to match the URL from Step 7
5. Make sure it ends with `/stripe/webhook`
6. Click **Save**

### Step 9: Test Again

1. In Stripe Dashboard → **Webhooks** → Your webhook
2. Click **Send test webhook**
3. Select event: `checkout.session.completed`
4. Click **Send test webhook**
5. Check CloudWatch logs immediately:
   - CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - You should see: `🔔 Stripe webhook received!`
   - And: `Processing Stripe webhook event: checkout.session.completed`
   - And: `✅ Credits granted: 20 to user...`

## Quick Checklist

- [ ] `/stripe` resource exists in API Gateway
- [ ] `/stripe/webhook` resource exists
- [ ] `POST` method exists on `/stripe/webhook` with Lambda integration
- [ ] `OPTIONS` method exists on `/stripe/webhook` (for CORS)
- [ ] CORS is enabled on `/stripe/webhook`
- [ ] API is deployed to your stage
- [ ] Stripe webhook URL matches your API Gateway endpoint + `/stripe/webhook`
- [ ] `STRIPE_WEBHOOK_SECRET` is set in Lambda environment variables

## Common Issues

**Still getting 404:**
- Make sure you deployed the API after creating the endpoint
- Verify the webhook URL in Stripe matches exactly (including `/stripe/webhook`)
- Check that the stage name matches (e.g., `prod` vs `default`)

**CORS errors:**
- Make sure `stripe-signature` is in the allowed headers
- Verify OPTIONS method exists and returns correct CORS headers

**Webhook received but credits not granted:**
- Check CloudWatch logs for errors
- Verify `STRIPE_WEBHOOK_SECRET` is set correctly
- Check that userId in metadata matches the frontend userId

