# Stripe Live Mode Setup Guide

This guide will help you switch from Stripe test mode to live mode for production.

## Prerequisites

- Stripe account with live mode enabled
- Access to AWS Lambda console
- Access to Stripe Dashboard

## Step 1: Get Your Live Stripe Keys

1. Log in to [Stripe Dashboard](https://dashboard.stripe.com/)
2. **Switch to Live Mode** (toggle in the top right corner)
3. Go to **Developers** → **API keys**
4. Copy your **Publishable key** (starts with `pk_live_...`)
5. Click **Reveal test key** to show your **Secret key** (starts with `sk_live_...`)
   - ⚠️ **Keep this secret!** Never commit it to version control

## Step 2: Update Lambda Environment Variables

1. Go to [AWS Lambda Console](https://console.aws.amazon.com/lambda/)
2. Find your Lambda function (likely named `LoveBehaviorTranslator-Function`)
3. Go to **Configuration** → **Environment variables**
4. Update the following variables:

   ```
   STRIPE_SECRET_KEY = sk_live_... (your live secret key)
   ```

   ⚠️ **Important:** Make sure you're using the **live** key (starts with `sk_live_`), not the test key (starts with `sk_test_`)

5. Click **Save**

## Step 3: Create Live Webhook Endpoint in Stripe

1. In Stripe Dashboard (still in **Live Mode**), go to **Developers** → **Webhooks**
2. Click **Add endpoint**
3. Enter your webhook URL:
   ```
   https://your-api-gateway-url.amazonaws.com/stripe/webhook
   ```
   (Replace `your-api-gateway-url` with your actual API Gateway URL)
4. Select events to listen to:
   - ✅ `checkout.session.completed` (required)
   - Optionally: `payment_intent.succeeded`, `payment_intent.payment_failed` for additional tracking
5. Click **Add endpoint**
6. **Copy the Signing secret** (starts with `whsec_...`)
   - This is shown only once! Save it securely.

## Step 4: Update Webhook Secret in Lambda

1. Go back to AWS Lambda Console
2. Go to **Configuration** → **Environment variables**
3. Add or update:
   ```
   STRIPE_WEBHOOK_SECRET = whsec_... (your live webhook signing secret)
   ```
4. Click **Save**

## Step 5: Verify API Gateway Endpoint

1. Go to [API Gateway Console](https://console.aws.amazon.com/apigateway/)
2. Find your API
3. Verify the webhook endpoint is configured:
   - Path: `/stripe/webhook`
   - Method: `POST`
   - Integration: Your Lambda function
4. **Important:** Make sure the endpoint is publicly accessible (not behind authentication)

## Step 6: Test Live Mode

### Test a Small Purchase

1. Go to your live website
2. Click "Unlock Clarity" or purchase credits
3. Use Stripe's [test card numbers](https://stripe.com/docs/testing) won't work in live mode
4. Use a **real card** with a small amount (e.g., $1.99 for the Starter Pack)
5. Complete the purchase
6. Verify:
   - ✅ Payment appears in Stripe Dashboard → **Payments** (Live mode)
   - ✅ Credits are granted to the user
   - ✅ Webhook event appears in Stripe Dashboard → **Developers** → **Webhooks** → **Events**
   - ✅ Admin email notification is sent (if configured)

### Monitor Webhook Events

1. In Stripe Dashboard → **Developers** → **Webhooks**
2. Click on your webhook endpoint
3. View **Recent events** to see webhook deliveries
4. Check for any failed deliveries (red status)
5. If failed, check CloudWatch logs for errors

## Step 7: Update Frontend (if needed)

The frontend code already uses the API URL from environment variables. Make sure:

1. Your production frontend has `VITE_API_BASE_URL` set to your live API Gateway URL
2. The success/cancel URLs in `CreditModal.tsx` point to your live domain

## Security Checklist

- [ ] Using live keys (starting with `sk_live_` and `pk_live_`)
- [ ] Webhook secret is set and matches Stripe Dashboard
- [ ] Webhook endpoint is publicly accessible
- [ ] No test keys in production environment variables
- [ ] Webhook signature verification is enabled (code checks `STRIPE_WEBHOOK_SECRET`)

## Troubleshooting

### Webhook Not Receiving Events

1. **Check API Gateway URL:**
   - Verify the webhook URL in Stripe matches your API Gateway endpoint
   - Test the endpoint manually: `curl https://your-api-url/stripe/webhook`

2. **Check CloudWatch Logs:**
   - Go to AWS Lambda → Your function → **Monitor** → **View CloudWatch logs**
   - Look for webhook-related errors

3. **Check Stripe Webhook Logs:**
   - Stripe Dashboard → **Developers** → **Webhooks** → Your endpoint → **Recent events**
   - Check for failed deliveries and error messages

### Payments Not Granting Credits

1. **Check Webhook Secret:**
   - Verify `STRIPE_WEBHOOK_SECRET` matches the signing secret from Stripe Dashboard
   - If mismatched, webhook verification will fail

2. **Check Metadata:**
   - Verify `userId` and `credits` are in session metadata
   - Check CloudWatch logs for metadata values

3. **Check DynamoDB:**
   - Verify credits are being written to DynamoDB
   - Check the `LoveBehaviorTranslatorUsers` table

### Test Mode vs Live Mode Confusion

- **Test keys** start with `sk_test_` and `pk_test_`
- **Live keys** start with `sk_live_` and `pk_live_`
- Make sure you're using the correct keys for the correct mode
- You can have both test and live webhooks configured

## Rollback Plan

If you need to rollback to test mode:

1. Update Lambda environment variable:
   ```
   STRIPE_SECRET_KEY = sk_test_... (your test key)
   STRIPE_WEBHOOK_SECRET = whsec_... (your test webhook secret)
   ```
2. Update Stripe Dashboard webhook URL to point to test endpoint (if different)
3. Test with test card numbers

## Support

If you encounter issues:
1. Check CloudWatch logs for detailed error messages
2. Check Stripe Dashboard → **Developers** → **Logs** for API errors
3. Verify all environment variables are set correctly
4. Ensure webhook endpoint is accessible from Stripe's servers

