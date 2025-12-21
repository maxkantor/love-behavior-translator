# Fix "Missing stripe-signature header" Error

## Problem

Stripe webhook returns `400 ERR` with error: `"Missing stripe-signature header"`

This means API Gateway is not passing the `stripe-signature` header to Lambda.

## Solution

### Step 1: Verify Lambda Proxy Integration

1. API Gateway → Your API → **Resources**
2. Select `/stripe/webhook` → `POST` method
3. Check **Integration type**:
   - ✅ Should be: **Lambda Function**
   - ✅ Should have: **Use Lambda Proxy integration** checked

If not, fix it:
1. Click **Integration Request**
2. **Integration type**: **Lambda Function**
3. ✅ Check **Use Lambda Proxy integration**
4. **Lambda Function**: `LoveBehaviorTranslatorFunction`
5. Click **Save**

### Step 2: Verify Header Passthrough

1. With `POST` method selected under `/stripe/webhook`
2. Click **Integration Request**
3. Scroll to **HTTP Headers** section
4. Ensure **Header passthrough** is enabled (default with proxy integration)
5. If you see **Header Mappings**, ensure `stripe-signature` is NOT mapped (it should pass through as-is)

### Step 3: Check API Gateway Method Request

1. Select `POST` method under `/stripe/webhook`
2. Click **Method Request**
3. Under **HTTP Request Headers**, you should see:
   - `stripe-signature` (or it should be allowed to pass through)
4. If `stripe-signature` is not listed, add it:
   - Click **Add header**
   - **Name**: `stripe-signature`
   - ✅ Check **Required** (optional, but recommended)
   - Click the checkmark to save

### Step 4: Deploy API

**CRITICAL:** After any changes, you must deploy:

1. **Actions** → **Deploy API**
2. **Deployment stage**: `prod` (or your stage)
3. Click **Deploy**
4. Wait 10-30 seconds

### Step 5: Test Webhook

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find your webhook endpoint
3. Click **Send test webhook**
4. Select event: `checkout.session.completed`
5. Click **Send test webhook**
6. Check status - should be `200 OK` (green) instead of `400 ERR` (red)

### Step 6: Check CloudWatch Logs

1. CloudWatch → **Log groups** → `/aws/lambda/LoveBehaviorTranslatorFunction`
2. Filter for: `"Stripe webhook"`
3. You should see:
   - `🔔 Stripe webhook received!`
   - `Header: stripe-signature = t=1766272949,v1=...`
   - `✅ Stripe signature present: t=1766272949,v1=...`

If you still see "Missing stripe-signature header", check:
- The header name in logs (might be `Stripe-Signature` with capital S)
- The Lambda function code has been updated (check `backend/dist/function.zip` timestamp)

## Alternative: Use Proxy Resource

If header passthrough still doesn't work, use a proxy resource:

1. API Gateway → Your API → **Resources**
2. Select `/stripe` resource
3. **Create resource** → **Resource name**: `proxy`
4. **Resource path**: `{proxy+}`
5. ✅ Check **Configure as proxy resource**
6. Click **Create resource**
7. Select `/stripe/{proxy+}` → **Create method** → `ANY`
8. **Integration type**: **Lambda Function**
9. ✅ Check **Use Lambda Proxy integration**
10. **Lambda Function**: `LoveBehaviorTranslatorFunction`
11. Click **Save** → **OK**
12. **Deploy API** (Step 4 above)

This ensures ALL requests under `/stripe/` (including `/stripe/webhook`) are passed to Lambda with all headers intact.

