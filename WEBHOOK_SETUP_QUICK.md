# Quick Fix: Set Up Stripe Webhook for Love Behavior Translator

## Step 1: Find Your API Gateway Endpoint URL

1. AWS Console → **API Gateway**
2. Select your API (`love-behavior-translator-api` or similar)
3. Click **Stages** in the left sidebar
4. Click on your stage (usually `prod` or `default`)
5. Copy the **Invoke URL** at the top
   - Example: `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod`
6. Your webhook URL will be: `{Invoke URL}/stripe/webhook`
   - Example: `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`

## Step 2: Create Webhook in Stripe

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Click **Add endpoint** (or **Add destination** if using new interface)
3. **Endpoint URL**: Paste your API Gateway webhook URL from Step 1
   - Must be: `https://{your-api-gateway-id}.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
   - ❌ NOT a Lambda Function URL
   - ❌ NOT the pet-behavior-translator webhook
4. **Description**: "Love Behavior Translator - Credit Purchase Webhook"
5. **Events to send**: Select `checkout.session.completed`
   - Click "Select events"
   - Search for "checkout.session.completed"
   - Check the box
   - Click "Add events"
6. Click **Add endpoint** (or **Add destination**)

## Step 3: Copy Webhook Signing Secret

1. After creating the webhook, you'll see a **Signing secret** (starts with `whsec_...`)
2. **Copy this secret** - you'll need it in the next step

## Step 4: Add Signing Secret to Lambda

1. AWS Console → **Lambda** → Your function (`LoveBehaviorTranslatorFunction`)
2. Scroll down to **Environment variables**
3. Click **Edit**
4. Add or update:
   - **Key**: `STRIPE_WEBHOOK_SECRET`
   - **Value**: Paste the signing secret from Step 3
5. Click **Save**

## Step 5: Verify Webhook Endpoint Exists in API Gateway

1. API Gateway → Your API → **Resources**
2. Check if `/stripe/webhook` exists:
   - Look for: `/stripe` → `/webhook` → `POST` method
3. If it doesn't exist:
   - Select `/stripe` resource → **Create resource**
   - Name: `webhook`, Path: `/webhook`
   - Click **Create resource**
   - Select `/stripe/webhook` → **Create method** → `POST`
   - Integration: **Lambda Function**
   - ✅ Check **Use Lambda Proxy integration**
   - Lambda Function: `LoveBehaviorTranslatorFunction`
   - Click **Save** → **OK**
4. **Deploy the API:**
   - Actions → **Deploy API**
   - Stage: `prod` (or your stage name)
   - Click **Deploy**

## Step 6: Test the Webhook

1. In Stripe Dashboard → **Webhooks** → Your new webhook
2. Click **Send test webhook**
3. Select event: `checkout.session.completed`
4. Click **Send test webhook**
5. Check CloudWatch logs immediately:
   - CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - You should see: `🔔 Stripe webhook received!`
   - And: `Processing Stripe webhook event: checkout.session.completed`

## Troubleshooting

**If webhook shows "Failed" in Stripe:**
- Check CloudWatch logs for errors
- Verify the endpoint URL is correct (API Gateway, not Lambda Function URL)
- Verify `/stripe/webhook` endpoint exists in API Gateway and is deployed

**If no events are received:**
- Make sure `checkout.session.completed` is selected in webhook events
- Verify the webhook is "Active" (not disabled)
- Check that payments are completing successfully in Stripe

**If credits still not granted:**
- Check CloudWatch logs for userId matching
- Verify `STRIPE_WEBHOOK_SECRET` is set correctly in Lambda
- Make sure the webhook endpoint is deployed in API Gateway

