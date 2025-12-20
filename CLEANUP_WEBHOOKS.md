# Clean Up Multiple Webhook Endpoints

## Why You Have 2 Webhooks

You have two webhook endpoints configured in Stripe:
1. **Love Behavior Translator** - `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook` ✅ (CORRECT)
2. **Another webhook** - `https://mltsxy8i59.execute-api.us-east-1.amazonaws.com/prod/api/webhooks/stri...` ❌ (WRONG - different API Gateway)

The second one is probably from:
- A different project (like "lucky-numbers-lab" or "pet-behavior-translator")
- An old/incorrect webhook that was created by mistake
- A different API Gateway endpoint

## The Problem

Stripe is sending events to BOTH webhooks, which is why you see 2 delivery attempts for each event. Only one of them should be active for "love-behavior-translator".

## The Solution

### Option 1: Delete the Incorrect Webhook (Recommended)

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find the webhook with the wrong URL:
   - `https://mltsxy8i59.execute-api.us-east-1.amazonaws.com/prod/api/webhooks/stri...`
   - Or any webhook that's NOT for "Love Behavior Translator"
3. Click on it
4. Click **"Edit destination"** or the settings icon
5. Click **"Delete destination"** or **"Delete"**
6. Confirm deletion

### Option 2: Disable the Incorrect Webhook

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find the incorrect webhook
3. Click on it
4. Toggle it to **"Inactive"** or disable it
5. This way you can keep it for reference but it won't receive events

### Option 3: Keep Both But Verify Which One Works

1. Check which webhook is getting 200 OK responses:
   - Stripe Dashboard → **Webhooks** → Each webhook → **Event deliveries**
   - Look for `checkout.session.completed` events
   - Check which one shows **200 OK** (green) vs **400 ERR** (red)
2. Keep the one that works, delete/disable the other

## Verify Your Correct Webhook

After cleanup, make sure your "Love Behavior Translator" webhook has:

1. **Endpoint URL**: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
2. **Status**: **Active** (green badge)
3. **Events**: Only `checkout.session.completed` selected
4. **Event deliveries**: Should show **200 OK** (green) after uploading Lambda fix

## After Cleanup

1. Upload the latest `backend/dist/function.zip` to Lambda
2. Test the webhook:
   - Stripe Dashboard → **Webhooks** → Your webhook → **"Send test webhook"**
   - Select `checkout.session.completed`
   - Click **"Send test webhook"**
3. Check status - should be **200 OK** (green)
4. Check CloudWatch logs - should see credit granting messages

## Why This Matters

Having multiple webhooks means:
- Stripe sends events to both (wasteful)
- You see duplicate delivery attempts
- One might be working while the other fails (confusing)
- Harder to debug which one is actually processing events

Keep only the correct webhook active for "love-behavior-translator".

