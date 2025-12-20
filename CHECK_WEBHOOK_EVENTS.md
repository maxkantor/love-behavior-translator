# Check What Events Are Being Sent

## The Real Issue

Your webhook IS working (43 events delivered, 0 failed), but credits aren't being granted. This means:

1. ✅ Webhook URL is correct
2. ✅ Webhook is receiving events
3. ❌ Either:
   - The events being sent are NOT `checkout.session.completed`
   - OR the Lambda function isn't processing them correctly
   - OR there's an error in the credit granting logic

## Check What Events Are Being Sent

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Event deliveries"** tab
3. Look at the events list
4. **Check the event types:**
   - Are they `checkout.session.completed`?
   - Or are they `charge.updated`, `payment_intent.succeeded`, etc.?

## The Problem

Your webhook is listening to **224 events** (as shown in the screenshot). This is too many! You only need `checkout.session.completed`.

**Other events like `charge.updated` won't grant credits** - only `checkout.session.completed` will.

## The Fix

### Step 1: Check Which Events Are Selected

1. Stripe Dashboard → **Webhooks** → Your webhook
2. Click **"Edit destination"**
3. Look at **"Events to send"** or **"Listening to"**
4. Click **"Show"** or **"Select events"** to see all events

### Step 2: Select ONLY `checkout.session.completed`

1. In the events selection:
   - **Uncheck all events** (or click "Clear all")
   - **Check ONLY**: `checkout.session.completed`
   - Click **Save**

### Step 3: Check CloudWatch Logs

1. CloudWatch → Log groups → `/aws/lambda/LoveBehaviorTranslatorFunction`
2. Filter for: `"Stripe webhook"` or `"checkout.session.completed"`
3. Look for:
   - `🔔 Stripe webhook received!`
   - `Processing Stripe webhook event: checkout.session.completed`
   - `Granting X credits to user...`

## Why This Matters

- `charge.updated` - Updates when charge status changes (doesn't grant credits)
- `payment_intent.succeeded` - Payment succeeded (doesn't grant credits)
- `checkout.session.completed` - **ONLY THIS ONE GRANTS CREDITS**

The webhook handler only processes `checkout.session.completed` events. All other events are ignored.

## Quick Test

1. Make a test purchase
2. Check Stripe Dashboard → **Webhooks** → **Event deliveries**
3. Look for `checkout.session.completed` event
4. Check its status:
   - **200 OK** = Webhook succeeded, check CloudWatch logs
   - **400 ERR** = Webhook failed, check error message
5. Check CloudWatch logs for credit granting messages

## If `checkout.session.completed` Events Are Being Sent But Credits Not Granted

Check CloudWatch logs for:
- `Processing Stripe webhook event: checkout.session.completed` - Event received
- `Session metadata: userId=..., credits=...` - Metadata found
- `Granting X credits to user...` - Credits being granted
- `✅ Credits granted: X to user...` - Success!

If you see errors, share them and we can fix them.

