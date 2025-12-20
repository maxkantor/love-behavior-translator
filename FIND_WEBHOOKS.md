# Find All Webhooks in Stripe

## How to See All Your Webhooks

1. Stripe Dashboard → **Developers** → **Webhooks**
2. You should see a list of ALL webhook endpoints
3. Look for:
   - **"Love Behavior Translator - Credit Purchase Webhook"** (correct one)
   - Any other webhooks (might be named differently)

## Why You See 2 Delivery Attempts in Events

When you look at an event (like `checkout.session.completed`), you see "Deliveries to webhook endpoints" showing 2 attempts. This means Stripe is sending the event to 2 different webhook endpoints.

## Find the Second Webhook

1. Stripe Dashboard → **Developers** → **Webhooks**
2. You should see a list of webhook endpoints
3. Count how many you see - if you see 2, note their names:
   - One should be "Love Behavior Translator - Credit Purchase Webhook"
   - The other might be:
     - "lucky-numbers-lab"
     - "pet-behavior-translator"
     - Or something else

## Check Each Webhook's URL

For each webhook, check the **Endpoint URL**:
- ✅ **Correct**: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
- ❌ **Wrong**: `https://mltsxy8i59.execute-api.us-east-1.amazonaws.com/prod/api/webhooks/stri...`
- ❌ **Wrong**: Any other URL

## Delete the Wrong Webhook

1. Stripe Dashboard → **Developers** → **Webhooks**
2. Find the webhook with the wrong URL (not the love-behavior-translator one)
3. Click on it
4. Click **"Edit destination"** or settings icon
5. Click **"Delete destination"** or **"Delete"**
6. Confirm deletion

## After Cleanup

You should only have ONE webhook:
- Name: "Love Behavior Translator - Credit Purchase Webhook"
- URL: `https://8dr22prv8l.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
- Events: Only `checkout.session.completed`

Then when you look at events, you'll only see 1 delivery attempt instead of 2.

