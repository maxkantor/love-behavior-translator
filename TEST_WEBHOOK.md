# How to Test Your Stripe Webhook

## Option 1: Test Webhook from Stripe Dashboard (Easiest)

1. **Go to Stripe Dashboard:**
   - Open: https://dashboard.stripe.com/test/webhooks
   - Make sure you're in **Test mode** (toggle in top right)

2. **Find Your Webhook:**
   - Look for your webhook endpoint (should show your API Gateway URL)
   - Click on it to open details

3. **Send Test Webhook:**
   - Click **"Send test webhook"** button (usually at the top right)
   - Select event: `checkout.session.completed`
   - Click **"Send test webhook"**

4. **Check CloudWatch Logs:**
   - AWS Console → CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
   - Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
   - You should see:
     - `🔔 Stripe webhook received!`
     - `Processing Stripe webhook event: checkout.session.completed`
     - `Granting X credits to user...`
     - `✅ Credits granted: X to user...`

## Option 2: Install Stripe CLI (For Local Testing)

If you want to test webhooks locally or forward them to your local machine:

1. **Install Stripe CLI:**
   - Windows: Download from https://github.com/stripe/stripe-cli/releases
   - Or use: `winget install stripe.stripe-cli`
   - Or use: `scoop install stripe`

2. **Login to Stripe:**
   ```powershell
   stripe login
   ```
   - This will open a browser to authenticate

3. **Forward Webhooks to Your Local Server:**
   ```powershell
   stripe listen --forward-to http://localhost:3000/stripe/webhook
   ```
   - This will show webhook events in real-time
   - You'll get a webhook signing secret (starts with `whsec_...`)

4. **Trigger Test Events:**
   ```powershell
   stripe trigger checkout.session.completed
   ```

## Option 3: Test with Real Payment (Recommended for Final Testing)

1. **Make a Test Purchase:**
   - Go to your website: https://lovebehaviortranslator.com
   - Click "Unlock Clarity"
   - Select a pack (e.g., Starter Pack $4.99)
   - Use test card: `4242 4242 4242 4242`
   - Any future expiry date, any CVC, any ZIP
   - Complete payment

2. **Check Stripe Dashboard:**
   - Go to: https://dashboard.stripe.com/test/payments
   - Find the payment you just made
   - Click on it → Check "Events" tab
   - You should see `checkout.session.completed` event

3. **Check Webhook Delivery:**
   - Go to: https://dashboard.stripe.com/test/webhooks
   - Click on your webhook
   - Check "Event deliveries" tab
   - You should see the `checkout.session.completed` event
   - Status should be "Succeeded" (green) or "Failed" (red)

4. **If Failed:**
   - Click on the failed event
   - Check the error message
   - Common issues:
     - Wrong webhook URL
     - Webhook endpoint doesn't exist in API Gateway
     - API Gateway not deployed
     - Lambda function error (check CloudWatch logs)

## Troubleshooting

**Webhook shows "Failed" in Stripe:**
- Check CloudWatch logs for errors
- Verify the endpoint URL is correct (API Gateway, not Lambda Function URL)
- Verify `/stripe/webhook` endpoint exists in API Gateway and is deployed
- Check that `STRIPE_WEBHOOK_SECRET` is set correctly in Lambda

**No events received:**
- Make sure `checkout.session.completed` is selected in webhook events
- Verify the webhook is "Active" (not disabled)
- Check that payments are completing successfully in Stripe

**Credits still not granted:**
- Check CloudWatch logs for userId matching
- Verify `STRIPE_WEBHOOK_SECRET` is set correctly in Lambda
- Make sure the webhook endpoint is deployed in API Gateway

