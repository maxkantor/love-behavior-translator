# Manual AWS Console Setup Guide

Complete step-by-step instructions for setting up **Love Behavior Translator** entirely from the **AWS Console** (no CDK/CLI required).

## Architecture Overview

This application uses:

- **React frontend** → AWS Amplify Hosting
- **.NET 8 Lambda** → Backend API handler
- **API Gateway (REST API v1)** → HTTP endpoints
- **DynamoDB** → Rate limiting + request logs (with TTL)
- **Secrets Manager** → Secure storage for OpenAI API key
- **S3** → Analysis artifacts storage
- **SES** → Optional email delivery
- **Route 53** → Domain management and DNS

> **Important**: Create all resources in **one AWS region** and stick to it throughout this guide.

---

## Prerequisites

Before starting, ensure you have:

- ✅ AWS account with permissions to create: IAM, Lambda, API Gateway, DynamoDB, S3, Secrets Manager, Amplify, SES, Route 53, ACM
- ✅ **OpenAI API key** (starts with `sk-...`)
- ✅ **.NET 8 SDK** installed locally (required to build Lambda zip)
- ✅ **Git** installed and configured
- ✅ **GitHub repository** created (or existing repo URL)

---

## Step 0: Build the Lambda Deployment Package

You must build the Lambda zip file locally before uploading it to AWS.

### Quick Build (Recommended)

From the repository root directory:

```powershell
.\backend\build.ps1
```

This should produce: `backend\dist\function.zip`

### Troubleshooting

#### Issue: PowerShell execution policy error

If you see an error about execution policy:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Then rerun `.\backend\build.ps1`

#### Issue: .NET 8 SDK not found

1. Download and install **.NET 8 SDK** (not just runtime)
2. Close and reopen your terminal
3. Verify installation:

```powershell
dotnet --info
```

You should see `.NET SDKs installed` with version `8.x.x`

#### Issue: Build script doesn't work

Run the build commands manually:

```powershell
# From repository root
dotnet restore .\backend\LoveBehaviorTranslator.sln
dotnet publish .\backend\src\LoveBehaviorTranslator.Function\LoveBehaviorTranslator.Function.csproj -c Release -o .\backend\dist\publish
Compress-Archive -Path .\backend\dist\publish\* -DestinationPath .\backend\dist\function.zip -Force
```

Verify the zip exists:

```powershell
Test-Path .\backend\dist\function.zip
```

Should return `True`.

---

## Step 1: Store OpenAI API Key in Secrets Manager

1. AWS Console → **Secrets Manager**
2. Click **Store a new secret**
3. **Secret type**: Select **"Other type of secret"**
4. Choose one of these formats:
   - **Plaintext**: Paste your `sk-...` key directly
   - **Key/value JSON**:
     - Key: `OPENAI_API_KEY`
     - Value: `sk-...`
5. **Secret name**: `love-behavior-translator/openai-api-key`
6. Click **Next** → **Next** → **Store**
7. After creation, open the secret and **copy its ARN** (you'll need this for Lambda environment variables)

---

## Step 2: Create DynamoDB Table

1. AWS Console → **DynamoDB** → **Tables** → **Create table**
2. **Table name**: `LoveBehaviorTranslator`
3. **Partition key**: `pk` (type: **String**)
4. **Sort key**: `sk` (type: **String**)
5. **Table settings**: **On-demand** capacity mode
6. Click **Create table**

### Enable TTL (Time to Live)

1. Open the `LoveBehaviorTranslator` table
2. Go to **Additional settings** (or **Table details**)
3. Find **Time to live (TTL)**
4. Enable TTL attribute: `ttl`
5. Save

> **Why TTL?** This app uses DynamoDB TTL to automatically expire:
> - Rate limit counters (per IP, per minute)
> - Request logs (after a retention period)

### 2.2 Create Users Table (For Credit System)

1. AWS Console → **DynamoDB** → **Tables** → **Create table**
2. **Table name**: `LoveBehaviorTranslatorUsers`
3. **Partition key**: `userId` (String)
4. **Table settings**: **On-demand** (or provisioned if preferred)
5. Click **Create table**

> **Note:** This table stores user credit balances and usage statistics for the credit-based monetization system.

### 2.3 Create Contacts Table (For Contact Form)

1. AWS Console → **DynamoDB** → **Tables** → **Create table**
2. **Table name**: `LoveBehaviorTranslatorContacts`
3. **Partition key**: `contactId` (String)
4. **Table settings**: **On-demand** (or provisioned if preferred)
5. **Additional settings** → **Time to live (TTL)**:
   - **TTL attribute name**: `ttl`
   - This will auto-delete contacts after 1 year
6. Click **Create table**

> **Note:** This table stores contact form submissions. Messages are automatically deleted after 1 year via TTL.

---

## Step 3: Create S3 Bucket

1. AWS Console → **S3** → **Create bucket**
2. **Bucket name**: Must be **globally unique** (no angle brackets `<` `>` allowed)
   - Examples:
     - `love-behavior-translator-artifacts-2025`
     - `love-behavior-translator-artifacts-maxkantor`
     - `love-behavior-translator-artifacts-abc123xyz`
3. **AWS Region**: Same region as your other resources
4. **Block Public Access**: Keep **ON** (default)
5. **Encryption**: Default (**SSE-S3**) is fine
6. Click **Create bucket**

### Optional: Set Lifecycle Rule

To automatically delete old artifacts:

1. Open your bucket → **Management** → **Lifecycle rules**
2. **Create lifecycle rule**
3. Name: `delete-old-artifacts`
4. **Expire current versions of objects**: `30` days
5. Create rule

---

## Step 4: Configure SES for Email

SES is required for:
- Contact form notifications (admin receives emails when users submit contact form)
- Admin reply emails (sending replies to users)
- Credit purchase notifications
- Optional: "Email me this analysis" feature

### 4.1 Navigate to SES

1. AWS Console → **Amazon SES** (make sure you're in the **same region** as your Lambda function)
2. If you don't see SES, search for "SES" in the AWS Console search bar

### 4.2 Verify Your Email Address

**Option A: Verify a Single Email Address (Recommended for Testing)**

1. In SES Console → **Verified identities** → **Create identity**
2. **Identity type**: Select **Email address**
3. **Email address**: Enter your email (e.g., `support@lovebehaviortranslator.com` or your personal email)
4. Click **Create identity**
5. **Check your email inbox** for a verification email from AWS
6. Click the verification link in the email
7. The email address will now show as **Verified** in SES

**Option B: Verify Your Domain (Recommended for Production)**

1. In SES Console → **Verified identities** → **Create identity**
2. **Identity type**: Select **Domain**
3. **Domain**: Enter your domain **without** `https://` or trailing slash
   - ✅ Correct: `lovebehaviortranslator.com`
   - ❌ Wrong: `https://lovebehaviortranslator.com/`
   - ❌ Wrong: `https://lovebehaviortranslator.com`
   - ❌ Wrong: `www.lovebehaviortranslator.com` (unless you want to verify www subdomain separately)
4. **Configuration set**: Leave empty (optional)
5. **DKIM signing**: Select **Easy DKIM** (recommended for better deliverability)
6. Click **Create identity**
7. **Copy DNS records from SES:**
   
   After clicking "Create identity", SES will show you a page titled **"Verify this domain"** with all the DNS records you need to add.
   
   **You'll see two sections:**
   
   a. **Domain verification record (TXT record):**
      - **Record name**: `_amazonses.lovebehaviortranslator.com` (or similar)
      - **Record type**: `TXT`
      - **Value**: A long string like `"v=spf1 include:amazonses.com ~all"` or similar
      - **Copy the entire value** (it's usually in a code box or text field)
   
   b. **DKIM records (3 CNAME records):**
      - **Record 1 name**: Something like `abc123._domainkey.lovebehaviortranslator.com`
      - **Record 1 value**: Something like `abc123.dkim.amazonses.com`
      - **Record 2 name**: Another `xyz789._domainkey.lovebehaviortranslator.com`
      - **Record 2 value**: `xyz789.dkim.amazonses.com`
      - **Record 3 name**: Another `def456._domainkey.lovebehaviortranslator.com`
      - **Record 3 value**: `def456.dkim.amazonses.com`
      - **Copy each name and value pair**
   
      > **Important Notes:**
      > - **If your domain is already verified**: The TXT record might not be visible in SES anymore, but it's already working in Route 53
      > - **To verify the TXT record exists**: Go to Route 53 → Your hosted zone → Look for a record named `_amazonses` with type `TXT`
      > - **DKIM records**: These are always visible in the "Publish DNS records" section under DKIM, even after verification
      > - **If you can't find the TXT record in SES**: It's likely already set up correctly since your domain is verified
   
8. **Add DNS Records in Route 53:**
   
   **If your domain is in Route 53:**
   
   a. **Copy the DNS records from SES:**
      - After clicking "Create identity", SES will show you a page with all the DNS records
      - You'll see something like:
        - **TXT record**: `_amazonses.lovebehaviortranslator.com` → `abc123...`
        - **CNAME records**: `abc123._domainkey.lovebehaviortranslator.com` → `abc123.dkim.amazonses.com`
        - (There will be 3 CNAME records with different prefixes)
   
   b. **Go to Route 53 Console:**
      - AWS Console → **Route 53** → **Hosted zones**
      - Click on your domain (e.g., `lovebehaviortranslator.com`)
      - You should see a table with existing DNS records (A, NS, SOA, CNAME, etc.)
   
   c. **Add the TXT record:**
      - Look for the **"Create record"** button above the records table (top right area)
      - Click **Create record**
      - **Record name**: 
        - If SES shows `_amazonses.lovebehaviortranslator.com`, enter just `_amazonses`
        - Route 53 will automatically append your domain name
      - **Record type**: Select **TXT**
      - **Value**: 
        - Paste the **entire value** from SES (including quotes if present)
        - It will look like: `"v=spf1 include:amazonses.com ~all"` or a long verification string
        - Make sure to include everything SES provided
      - **TTL**: Leave as default (300) or set to 300
      - Click **Create records**
   
   d. **Add the CNAME records (3 records):**
      - Click **Create record** (repeat for each of the 3 CNAME records)
      - **Record name**: 
        - If SES shows `abc123._domainkey.lovebehaviortranslator.com`, enter just `abc123._domainkey`
        - Route 53 will automatically append your domain name
        - Use the **exact prefix** from SES (e.g., `abc123._domainkey`, `xyz789._domainkey`, `def456._domainkey`)
      - **Record type**: Select **CNAME**
      - **Value**: 
        - Paste the **target** from SES (e.g., `abc123.dkim.amazonses.com`)
        - Make sure to include the trailing dot if SES shows it (`.dkim.amazonses.com.`)
        - Usually it's something like: `abc123.dkim.amazonses.com`
      - **TTL**: Leave as default (300) or set to 300
      - Click **Create records**
      - **Repeat for all 3 CNAME records** (each will have a different prefix like `abc123`, `xyz789`, `def456`)
   
   e. **Verify the records are added:**
      - You should see all 4 records in your Route 53 hosted zone:
        - 1 TXT record: `_amazonses`
        - 3 CNAME records: `[prefix]._domainkey`
   
   **If your domain is NOT in Route 53 (using another DNS provider):**
   
   - Go to your DNS provider's console (GoDaddy, Namecheap, Cloudflare, etc.)
   - Add the same DNS records (TXT and 3 CNAME records) as shown in SES
   - Use the exact record names and values provided by SES
   - Save the records
   
9. **Wait for DNS propagation:**
   - DNS changes typically take 5-15 minutes to propagate
   - You can check if records are live using:
     - `nslookup -type=TXT _amazonses.lovebehaviortranslator.com`
     - Or use online tools like `dnschecker.org`
   
10. **Verify in SES:**
    - Go back to SES Console → **Verified identities**
    - Find your domain in the list
    - Click **Verify** (or it may auto-verify once DNS records are detected)
    - Status should change to **Verified** ✅
   
11. **Once verified, you can send from any email address** on that domain (e.g., `support@lovebehaviortranslator.com`, `noreply@lovebehaviortranslator.com`, etc.)

### 4.3 Request Production Access (Move Out of Sandbox)

**Important**: By default, SES starts in **Sandbox mode**, which means:
- You can only send emails **TO verified email addresses**
- You can send up to 200 emails per day
- You can send 1 email per second

**To request production access:**

1. In SES Console → **Account dashboard** (or **Sending statistics**)
2. Look for **Account status** section
3. If it shows **Sandbox**, click **Request production access**
4. Fill out the form:
   - **Mail type**: Select **Transactional** (for contact forms, replies, notifications)
   - **Website URL**: Your website URL (e.g., `https://lovebehaviortranslator.com`)
   - **Use case description**: 
     ```
     We use SES to send:
     - Contact form submission notifications to admin
     - Reply emails to users who contact us
     - Credit purchase notifications
     - Optional: Analysis results to users who request email delivery
     ```
   - **Expected sending volume**: Estimate (e.g., "100-500 emails per day")
   - **Compliance**: Check the boxes for:
     - ✅ I have read and agree to the AWS Service Terms
     - ✅ I will only send to recipients who have opted-in
5. Click **Submit request**
6. AWS typically approves within 24-48 hours
7. Once approved, you can send to **any email address** (not just verified ones)

### 4.4 Configure Lambda Environment Variable

1. AWS Console → **Lambda** → Select `LoveBehaviorTranslatorFunction`
2. **Configuration** → **Environment variables** → **Edit**
3. Add or update:
   - **Key**: `SES_FROM_EMAIL`
   - **Value**: `support@lovebehaviortranslator.com` (or your preferred email address on your verified domain)
4. Click **Save**

> **Important**: 
> - If you verified a **domain** (`lovebehaviortranslator.com`), you can use any email on that domain:
>   - ✅ `support@lovebehaviortranslator.com` (recommended)
>   - ✅ `noreply@lovebehaviortranslator.com`
>   - ✅ `admin@lovebehaviortranslator.com`
> - If you verified a **single email**, you must use that exact email address
> - The email must be **verified** before you can send from it
> - **All reply emails will be sent FROM this address**, so choose a professional email like `support@`

### 4.5 Test Email Sending

**Option 1: Test via Contact Form**

1. Go to your website
2. Click **Need Help?**
3. Fill out the contact form and submit
4. Check your email (the one set as `SES_FROM_EMAIL`) for a notification

**Option 2: Test via Admin Dashboard**

1. Log into admin dashboard
2. Go to **Contact Messages** section
3. Click **Reply** on a contact message
4. Type a reply and send
5. Check the user's email for the reply

**Option 3: Test via AWS Console**

1. SES Console → **Verified identities**
2. Select your verified email
3. Click **Send test email**
4. Enter a test recipient (must be verified if in Sandbox)
5. Send and check recipient's inbox

### 4.6 Troubleshooting

**Error: "Email address not verified"**
- Solution: Verify the email address in SES Console → Verified identities

**Error: "SES_FROM_EMAIL not configured"**
- Solution: Set the `SES_FROM_EMAIL` environment variable in Lambda

**Error: "Message rejected: Email address is not verified"**
- Solution: You're in Sandbox mode. Either:
  - Verify the recipient email address in SES, OR
  - Request production access (see Step 4.3)

**Error: "Daily sending quota exceeded"**
- Solution: You've hit the 200 emails/day limit in Sandbox. Request production access.

**Emails going to spam**
- Solution: 
  - Verify your domain (not just email) and set up DKIM
  - Use a professional email address (e.g., `support@` instead of `test@`)
  - Include proper email content (avoid spam trigger words)

### 4.7 Best Practices

---

## Step 5: Configure Stripe for Payments

Stripe is required for users to purchase credits through the "Unlock Clarity" feature.

### 5.1 Create Stripe Account

1. Go to [Stripe Dashboard](https://dashboard.stripe.com/register)
2. Sign up with your email address
3. Complete business verification:
   - Business type and details
   - Business address
   - Bank account information (for payouts)
4. Verify your email address

### 5.2 Get Stripe API Keys

1. In Stripe Dashboard → **Developers** → **API keys**
2. You'll see two sets of keys:
   - **Test mode keys** (for development/testing)
   - **Live mode keys** (for production)
3. Copy your **Secret key** (starts with `sk_test_` for test mode or `sk_live_` for live mode)
4. Copy your **Publishable key** (starts with `pk_test_` or `pk_live_`) - you'll need this later for frontend if needed

> **Important**: 
> - Use **test mode** keys during development
> - Switch to **live mode** keys only when ready for production
> - Never commit API keys to git

### 5.3 Store Stripe Secret Key in Secrets Manager

1. AWS Console → **Secrets Manager**
2. **Store a new secret** (or edit existing `love-behavior-translator/app-secrets`)
3. **Secret type**: **Other type of secret** → **Plaintext** or **Key/value**
4. If using existing secret, add a new key:
   - **Key**: `STRIPE_SECRET_KEY`
   - **Value**: Your Stripe secret key (e.g., `sk_test_...` or `sk_live_...`)
5. If creating new secret:
   - **Secret name**: `love-behavior-translator/app-secrets`
   - **Value**: JSON format:
     ```json
     {
       "OPENAI_API_KEY": "sk-...",
       "ADMIN_PASSWORD": "your-admin-password",
       "STRIPE_SECRET_KEY": "sk_test_..."
     }
     ```
6. Click **Store** (or **Save**)

### 5.4 Configure Lambda Environment Variables

1. AWS Console → **Lambda** → `LoveBehaviorTranslatorFunction`
2. **Configuration** → **Environment variables** → **Edit**
3. Add:
   - **Key**: `STRIPE_SECRET_KEY`
   - **Value**: Your Stripe secret key directly (e.g., `sk_test_...`)
   - **OR** use the secret ARN if you prefer (see below)
4. Click **Save**

> **Alternative**: You can also read from Secrets Manager in code, but environment variables are simpler for Lambda.

### 5.5 Set Up Stripe Webhook

**First, find your API Gateway endpoint URL:**

1. AWS Console → **API Gateway**
2. Select your API (`love-behavior-translator-api`)
3. Click **Stages** in the left sidebar
4. Click on your stage (usually `prod` or `default`)
5. You'll see **Invoke URL** at the top (e.g., `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod`)
6. **Copy this URL** - this is your API Gateway endpoint URL

**For Production:**

1. In Stripe Dashboard → **Developers** → **Webhooks**
2. Click **Add endpoint**
3. **Endpoint URL**: `{YOUR_INVOKE_URL}/stripe/webhook`
   - Example: `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod/stripe/webhook`
   - Replace `{YOUR_INVOKE_URL}` with the Invoke URL from step 5 above
4. **Description**: "Love Behavior Translator - Credit Purchase Webhook"
5. **Events to send**: Select `checkout.session.completed`
6. Click **Add endpoint**
7. **Copy the Signing secret** (starts with `whsec_...`)
8. Add to Lambda environment variable:
   - **Key**: `STRIPE_WEBHOOK_SECRET`
   - **Value**: The webhook signing secret

**For Testing (Local Development):**

1. Install Stripe CLI: `stripe listen --forward-to http://localhost:3000/stripe/webhook`
2. Use the webhook secret provided by the CLI

### 5.6 Create API Gateway Endpoints for Stripe

#### 5.6.1 Create `/stripe/create-checkout-session` Endpoint

**⚠️ IMPORTANT: This endpoint must exist or you'll get a 404 error!**

**Option A: Create Specific Endpoint (Recommended)**

1. API Gateway → Your API → **Resources**
2. Check if `/stripe` resource exists:
   - If it exists, skip to step 6
   - If it doesn't exist, continue with step 3
3. **Create resource** (to create `/stripe`)
4. **Resource name**: `stripe`
5. **Resource path**: `/stripe`
6. Click **Create resource**
7. Select `/stripe` resource → **Create resource** (to create `/create-checkout-session` under `/stripe`)
8. **Resource name**: `create-checkout-session`
9. **Resource path**: `/create-checkout-session` (API Gateway will show the full path as `/stripe/create-checkout-session`)
10. Click **Create resource**
11. **Verify the full path**: You should now see `/stripe/create-checkout-session` in the resource tree
12. Select `/stripe/create-checkout-session` → **Create method** → `POST`
13. **Integration type**: Select **Lambda Function** (from dropdown)
14. ✅ **IMPORTANT**: Check the box **"Use Lambda Proxy integration"** (this is critical!)
15. **Lambda Function**: Type or select `LoveBehaviorTranslatorFunction`
16. **Lambda Region**: Should auto-detect (e.g., `us-east-1`)
17. Click **Save**
18. **If prompted**: Click **OK** to grant API Gateway permission to invoke Lambda
19. **Verify the integration:**
    - You should see the method execution flow showing:
      - Method Request → Integration Request → Integration Response → Method Response
    - Click on **Integration Request** - it should show:
      - Integration type: **Lambda Function**
      - Use Lambda Proxy integration: **Enabled** ✅
      - Lambda Function: `LoveBehaviorTranslatorFunction`

**If you see "No integration defined":**
- Click on the `POST` method
- Click **Integration Request** (or the integration section)
- If it's empty, click **Edit** or **Create integration**
- Set Integration type: **Lambda Function**
- ✅ Check **Use Lambda Proxy integration**
- Lambda Function: `LoveBehaviorTranslatorFunction`
- Click **Save**

**Option B: Use Proxy Resource (Easier - Catches All Stripe Routes)**

If Option A doesn't work or you want a simpler setup:

1. API Gateway → Your API → **Resources**
2. Check if `/stripe` resource exists:
   - If it exists, select it
   - If it doesn't exist, create it: **Create resource** → Name: `stripe`, Path: `/stripe` → **Create resource**
3. With `/stripe` selected → **Create resource**
4. **Resource name**: `proxy`
5. **Resource path**: `{proxy+}`
6. ✅ **Check "Configure as proxy resource"**
7. Click **Create resource**
8. You should now see `/stripe/{proxy+}` in the resource tree
9. Select `/stripe/{proxy+}` → **Create method** → `ANY`
10. **Integration type**: Select **Lambda Function** (from dropdown)
11. ✅ **IMPORTANT**: Check the box **"Use Lambda Proxy integration"** (this is critical!)
12. **Lambda Function**: Type or select `LoveBehaviorTranslatorFunction`
13. **Lambda Region**: Should auto-detect (e.g., `us-east-1`)
14. Click **Save**
15. **If prompted**: Click **OK** to grant API Gateway permission to invoke Lambda
16. **Verify the integration:**
    - Click on the `ANY` method
    - You should see the method execution flow
    - Click **Integration Request** - it should show Lambda Proxy integration is enabled

**If you see "No integration defined":**
- Click on the `ANY` method
- Click **Integration Request**
- Click **Edit** or **Create integration**
- Set Integration type: **Lambda Function**
- ✅ Check **Use Lambda Proxy integration**
- Lambda Function: `LoveBehaviorTranslatorFunction`
- Click **Save**

> **Note**: Option B (proxy) will route ALL requests to `/stripe/*` to Lambda, which then handles routing internally. This is simpler but less explicit.

#### 5.6.2 Create `/stripe/webhook` Endpoint

1. Select `/stripe` resource → **Create resource**
2. **Resource name**: `webhook`
3. **Resource path**: `/webhook`
4. Click **Create resource**
5. Select `/stripe/webhook` → **Create method** → `POST`
6. **Integration type**: **Lambda Function**
7. ✅ Check **Use Lambda Proxy integration**
8. **Lambda Function**: `LoveBehaviorTranslatorFunction`
9. Click **Save** → **OK**

> **Important**: The webhook endpoint should **NOT** require authentication. Stripe will sign the requests.

#### 5.6.3 Enable CORS for Stripe Endpoints (REQUIRED!)

**⚠️ CORS errors will occur if you skip this step!**

**Use API Gateway's built-in CORS (Recommended - Most Reliable):**

For `/stripe/create-checkout-session`:

1. API Gateway → Your API → **Resources**
2. Select `/stripe/create-checkout-session` resource
3. Click **Actions** → **Enable CORS**
4. Configure:
   - **Access-Control-Allow-Origin**: `*` (or `https://lovebehaviortranslator.com` for production)
   - **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id`
   - **Access-Control-Allow-Methods**: `POST,OPTIONS`
   - Leave other fields as default
5. Click **Enable CORS and replace existing CORS headers**
6. Click **Yes, replace existing values** when prompted
7. **IMPORTANT**: API Gateway will auto-create an OPTIONS method - this is correct!

For `/stripe/webhook`:

1. Select `/stripe/webhook` resource
2. Click **Actions** → **Enable CORS**
3. Configure:
   - **Access-Control-Allow-Origin**: `*`
   - **Access-Control-Allow-Headers**: `Content-Type,stripe-signature`
   - **Access-Control-Allow-Methods**: `POST,OPTIONS`
4. Click **Enable CORS and replace existing CORS headers**
5. Click **Yes, replace existing values**

**After enabling CORS:**
1. **Deploy the API** (see Step 7.5) - **This is critical!**
2. Wait 10-30 seconds for the deployment to propagate
3. Clear your browser cache (Ctrl+Shift+R or Cmd+Shift+R)
4. Test the endpoint again

> **Note**: API Gateway's built-in CORS is more reliable than Lambda-based OPTIONS handling because it handles the preflight request at the API Gateway level, before it reaches Lambda.

### 5.7 Test Stripe Integration

1. **Test Mode**: Use test card numbers from [Stripe Testing](https://stripe.com/docs/testing)
   - Success: `4242 4242 4242 4242`
   - Decline: `4000 0000 0000 0002`
2. Go to your website → Click **Unlock Clarity**
3. Select a credit pack → Click **Get [Pack Name]**
4. You should be redirected to Stripe Checkout
5. Use test card: `4242 4242 4242 4242`, any future expiry, any CVC, any ZIP
6. Complete payment
7. You should be redirected back with `?payment=success`
8. Check that credits were added to your account
9. Check Stripe Dashboard → **Payments** to see the test payment

### 5.8 Go Live with Stripe

When ready for production:

1. Complete Stripe account verification (business details, bank account)
2. Switch to **Live mode** in Stripe Dashboard
3. Update Lambda environment variable `STRIPE_SECRET_KEY` with live key (`sk_live_...`)
4. Update webhook endpoint URL to production API Gateway URL
5. Update webhook signing secret in Lambda (`STRIPE_WEBHOOK_SECRET`)
6. Test with a small real payment first

### 5.9 Troubleshooting

**Error: "Stripe not configured"**
- Solution: Set `STRIPE_SECRET_KEY` in Lambda environment variables

**Error: "Webhook signature verification failed"**
- Solution: Ensure `STRIPE_WEBHOOK_SECRET` matches the webhook signing secret in Stripe Dashboard

**Credits not added after payment**
- Check CloudWatch logs for webhook processing errors
- Verify webhook endpoint is receiving events in Stripe Dashboard
- Ensure `checkout.session.completed` event is selected in webhook configuration

**Payment succeeds but redirect fails**
- Check `successUrl` and `cancelUrl` in checkout session creation
- Ensure URLs are absolute (include `https://`)

**404 Error: "Not found" when clicking "Get [Pack Name]"**

**If CloudWatch logs show NO request to `/stripe/create-checkout-session`**, this means the endpoint doesn't exist in API Gateway or isn't deployed.

**Quick Fix Checklist:**

1. **Verify endpoint exists in API Gateway:**
   - API Gateway → Your API → **Resources**
   - Look in the resource tree for:
     ```
     /
     ├── /stripe
     │   └── /create-checkout-session
     │       └── POST (method)
     ```
   - **If `/stripe` doesn't exist:**
     - Click **Create resource** (at root `/`)
     - Name: `stripe`, Path: `/stripe`
     - Click **Create resource**
   - **If `/stripe` exists but `/create-checkout-session` doesn't:**
     - Select `/stripe` → **Create resource**
     - Name: `create-checkout-session`
     - Path: `/create-checkout-session`
     - Click **Create resource**
   - **If `/stripe/create-checkout-session` exists but has no POST method:**
     - Select `/stripe/create-checkout-session` → **Create method** → `POST`
     - Integration: **Lambda Function**
     - ✅ Check **Use Lambda Proxy integration**
     - Lambda: `LoveBehaviorTranslatorFunction`
     - Click **Save** → **OK**

2. **Enable CORS:**
   - Select `/stripe/create-checkout-session` → **Actions** → **Enable CORS**
   - Access-Control-Allow-Origin: `*`
   - Access-Control-Allow-Headers: `Content-Type,Authorization,x-user-id`
   - Access-Control-Allow-Methods: `POST,OPTIONS`
   - Click **Enable CORS and replace existing CORS headers**
   - Click **Yes, replace existing values**

3. **Deploy the API (CRITICAL!):**
   - **Actions** → **Deploy API**
   - Select your stage (e.g., `prod`)
   - Click **Deploy**
   - **Wait 10-30 seconds** for propagation

4. **Verify in CloudWatch:**
   - Try clicking "Get [Pack Name]" again
   - Check CloudWatch logs - you should NOW see:
     ```
     Request: POST /stripe/create-checkout-session (raw: /stripe/create-checkout-session)
     ```
   - If you still don't see this log, the endpoint still doesn't exist or isn't deployed

5. **Test directly with curl:**
   ```bash
   curl -X POST https://8dr22prv81.execute-api.us-east-1.amazonaws.com/prod/stripe/create-checkout-session \
     -H "Content-Type: application/json" \
     -d '{"credits":20,"price":4.99}'
   ```
   - **404 response** = endpoint doesn't exist or not deployed
   - **500 "Stripe not configured"** = endpoint exists, but `STRIPE_SECRET_KEY` missing
   - **200 with JSON** = endpoint works!

**CORS Error: "No 'Access-Control-Allow-Origin' header is present"**

This is the most common issue. Follow these steps in order:

1. **Verify OPTIONS method exists:**
   - API Gateway → Resources → `/stripe/create-checkout-session`
   - You should see both `POST` and `OPTIONS` methods listed
   - If OPTIONS is missing, create it (see Step 5.6.3)

2. **Deploy the API (CRITICAL!):**
   - API Gateway → **Actions** → **Deploy API**
   - Select your stage (e.g., `prod`)
   - Click **Deploy**
   - **This must be done after creating any new methods!**

3. **Wait for propagation:**
   - Wait 10-30 seconds after deploying
   - API Gateway changes can take a moment to propagate

4. **Clear browser cache:**
   - Hard refresh: `Ctrl+Shift+R` (Windows) or `Cmd+Shift+R` (Mac)
   - Or clear browser cache completely

5. **Verify in API Gateway:**
   - Check that both POST and OPTIONS methods show "Lambda Proxy" integration
   - Both should point to `LoveBehaviorTranslatorFunction`

6. **Check CloudWatch logs:**
   - Lambda → `LoveBehaviorTranslatorFunction` → **Monitor** → **View CloudWatch logs**
   - Look for OPTIONS requests - if you don't see them, API Gateway isn't routing them

7. **Alternative: Use API Gateway CORS:**
   - If Lambda OPTIONS still doesn't work, use Option B in Step 5.6.3 (Enable CORS directly in API Gateway)

---

### 4.7 Best Practices

1. **Use a domain** instead of a single email for better deliverability
2. **Set up DKIM** signing (automatic with domain verification)
3. **Monitor sending statistics** in SES Console
4. **Set up bounce/complaint handling** (optional, for production)
5. **Use a dedicated email** like `support@` or `noreply@` for automated emails

---

## Step 5: Create IAM Role for Lambda

### 5.1 Create the Role

1. AWS Console → **IAM** → **Roles** → **Create role**
2. **Trusted entity type**: **AWS service**
3. **Use case**: **Lambda**
4. **Permissions**: Attach managed policy:
   - `AWSLambdaBasicExecutionRole`
5. **Role name**: `LoveBehaviorTranslatorLambdaRole`
6. Click **Create role**

### 5.2 Add Inline Policy

1. Open `LoveBehaviorTranslatorLambdaRole`
2. **Add permissions** → **Create inline policy**
3. Click **JSON** tab
4. Paste this policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "DynamoRW",
      "Effect": "Allow",
      "Action": [
        "dynamodb:PutItem",
        "dynamodb:UpdateItem",
        "dynamodb:GetItem",
        "dynamodb:Query",
        "dynamodb:Scan"
      ],
      "Resource": "*"
    },
    {
      "Sid": "S3RW",
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject"
      ],
      "Resource": "*"
    },
    {
      "Sid": "SecretsRead",
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "*"
    },
    {
      "Sid": "SesSend",
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    }
  ]
}
```

5. **Policy name**: `LoveBehaviorTranslatorInline`
6. Click **Create policy**

> **Security Note**: Later, you can tighten `Resource: "*"` to specific ARNs for better security.

---

## Step 6: Create Lambda Function

### 6.1 Create Function

1. AWS Console → **Lambda** → **Create function**
2. **Author from scratch**
3. **Function name**: `LoveBehaviorTranslatorFunction`
4. **Runtime**: **.NET 8**
5. **Architecture**: `x86_64`
6. **Permissions**: **Use an existing role** → Select `LoveBehaviorTranslatorLambdaRole`
7. Click **Create function**

### 6.2 Upload Deployment Package

1. In the function → **Code** tab
2. **Upload from** → **.zip file**
3. Click **Upload** and select `backend\dist\function.zip`
4. Wait for upload to complete

### 6.3 Set Handler

1. **Configuration** → **Runtime settings** → **Edit**
2. **Handler**: Set to:

```
LoveBehaviorTranslator.Function::LoveBehaviorTranslator.Function.Function::FunctionHandler
```

3. Click **Save**

### 6.4 Configure Environment Variables

1. **Configuration** → **Environment variables** → **Edit**
2. Add these variables:

| Variable Name | Value | Notes |
|--------------|-------|-------|
| `TABLE_NAME` | `LoveBehaviorTranslator` | DynamoDB table name |
| `ARTIFACTS_BUCKET` | `your-bucket-name` | Your S3 bucket name from Step 3 |
| `OPENAI_SECRET_ARN` | `arn:aws:secretsmanager:...` | ARN from Secrets Manager (Step 1) |
| `OPENAI_MODEL` | `gpt-4o-mini` | Or your preferred OpenAI model |
| `RATE_LIMIT_PER_MINUTE` | `10` | Max requests per minute per IP |
| `RATE_LIMIT_BURST` | `5` | Burst allowance |
| `SES_FROM_EMAIL` | `your@email.com` | Verified SES email (optional, leave empty if not using) |

3. Click **Save**

### 6.5 Configure Timeout and Memory

1. **Configuration** → **General configuration** → **Edit**
2. **Memory**: `1024` MB
3. **Timeout**: `29` seconds
4. Click **Save**

---

## Step 7: Create API Gateway REST API

### 7.1 Create API

1. AWS Console → **API Gateway**
2. **Create API** → **REST API** (NOT HTTP API)
3. **New API**
4. **API name**: `love-behavior-translator-api`
5. Click **Create API**

### 7.2 Create `/health` Endpoint

1. **Resources** → **Create resource**
2. **Resource name**: `health`
3. **Resource path**: `/health`
4. Click **Create resource**
5. Select `/health` → **Create method** → `GET`
6. **Integration type**: **Lambda Function**
7. ✅ Check **Use Lambda Proxy integration**
8. **Lambda Function**: `LoveBehaviorTranslatorFunction`
9. Click **Save** → **OK** (when prompted to grant permissions)

### 7.3 Create `/analyze` Endpoint

1. **Resources** → **Create resource**
2. **Resource name**: `analyze`
3. **Resource path**: `/analyze`
4. Click **Create resource**
5. Select `/analyze` → **Create method** → `POST`
6. **Integration type**: **Lambda Function**
7. ✅ Check **Use Lambda Proxy integration**
8. **Lambda Function**: `LoveBehaviorTranslatorFunction`
9. Click **Save** → **OK** (when prompted to grant permissions)

### 7.3.1 Create `/credits` Endpoint

1. **Resources** → **Create resource**
2. **Resource name**: `credits`
3. **Resource path**: `/credits`
4. Click **Create resource**
5. Select `/credits` → **Create method** → `GET`
6. **Integration type**: **Lambda Function**
7. ✅ Check **Use Lambda Proxy integration**
8. **Lambda Function**: `LoveBehaviorTranslatorFunction`
9. Click **Save** → **OK** (when prompted to grant permissions)

**Important:** Also create an `OPTIONS` method for CORS preflight:
1. Select `/credits` → **Create method** → `OPTIONS`
2. **Integration type**: **Lambda Function**
3. ✅ Check **Use Lambda Proxy integration**
4. **Lambda Function**: `LoveBehaviorTranslatorFunction`
5. Click **Save** → **OK**

### 7.3.3 Create `/admin` Proxy Resource (For Admin Routes)

1. **Resources** → **Create resource**
2. **Resource name**: `admin`
3. **Resource path**: `/admin`
4. Click **Create resource**
5. With `/admin` selected → **Create resource** again
6. **Resource name**: `proxy`
7. **Resource path**: `{proxy+}`
8. ✅ Check **Configure as proxy resource**
9. Click **Create resource**
10. Select `/admin/{proxy+}` → **Create method** → `ANY`
11. **Integration type**: **Lambda Function**
12. ✅ Check **Use Lambda Proxy integration**
13. **Lambda Function**: `LoveBehaviorTranslatorFunction`
14. Click **Save** → **OK** (when prompted to grant permissions)

**Note:** This catch-all proxy will route all `/admin/*` requests (including `/admin/login`, `/admin/users`, etc.) to Lambda, which handles routing internally.

### 7.4 Configure CORS

**IMPORTANT:** The Lambda function handles CORS automatically, so you have two options:

#### Option A: Let Lambda Handle CORS (Recommended)

1. **Do NOT enable CORS in API Gateway** - the Lambda function returns CORS headers automatically
2. If OPTIONS methods were auto-created, you can delete them (Lambda handles OPTIONS requests)
3. Skip to Step 7.5

#### Option B: Enable CORS in API Gateway

If you prefer API Gateway to handle CORS:

For each resource (`/`, `/health`, `/analyze`, `/credits`, `/contact`, `/admin/*`):

1. Select the resource in the left pane
2. Click **Actions** → **Enable CORS**
3. Configure:
   - **Access-Control-Allow-Origin**: `*` (tighten later to your Amplify domain)
   - **Access-Control-Allow-Methods**: `GET,POST,PUT,OPTIONS`
   - **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id`
4. Click **Enable CORS and replace existing CORS headers**
5. **After enabling CORS, you must manually update each OPTIONS method:**
   - Click on the `OPTIONS` method under each resource
   - Go to **Method Response**
   - Under **200**, edit **Response Headers**
   - Ensure `Access-Control-Allow-Headers` includes `x-user-id`
   - Go to **Integration Response**
   - Under **200**, edit **Header Mappings**
   - Set `Access-Control-Allow-Headers` to: `'Content-Type,Authorization,x-user-id'`

### 7.5 Deploy API (CRITICAL!)

**⚠️ You MUST deploy the API after creating any new endpoints or methods!**

1. **Actions** → **Deploy API**
2. **Deployment stage**: Select your stage (e.g., `prod` or create a new one)
3. **Deployment description**: (optional) e.g., "Added Stripe endpoints"
4. Click **Deploy**
5. **Wait 10-30 seconds** for the deployment to propagate

> **Important**: If you don't deploy after creating endpoints, they won't be accessible and you'll get 404 errors!

1. **Actions** → **Deploy API**
2. **Deployment stage**: **New stage**
3. **Stage name**: `prod`
4. Click **Deploy**

5. **Copy the Invoke URL** (e.g., `https://xxxx.execute-api.us-east-1.amazonaws.com/prod`)

### 7.6 Test the API

Open the Invoke URL in a browser or use curl:

```bash
curl https://xxxx.execute-api.us-east-1.amazonaws.com/prod/health
```

Should return: `{"status":"healthy"}`

---

## Step 8: Deploy Frontend with AWS Amplify

### 8.1 Push Code to GitHub

Before Amplify can deploy, your code must be in GitHub:

```powershell
# From repository root
git init
git branch -M main
git add -A
git commit -m "Initial commit"

# If you haven't set origin yet:
git remote add origin https://github.com/maxkantor/love-behavior-translator.git

# Push to GitHub
git push -u origin main
```

> **Note**: If you need to update the remote URL:
> ```powershell
> git remote set-url origin https://github.com/maxkantor/love-behavior-translator.git
> ```

### 8.2 Connect Amplify to GitHub

1. AWS Console → **AWS Amplify** → **Host web app**
2. **Git provider**: **GitHub**
3. **Authorize** AWS Amplify to access your GitHub account (if first time)
4. **Repository**: Select `maxkantor/love-behavior-translator`
5. **Branch**: `main`
6. Click **Next**

### 8.3 Configure Build Settings

1. Amplify should automatically detect `amplify.yml`
2. If not, verify the file exists in your repo root

### 8.4 Add Environment Variable

**First, find your API Gateway endpoint URL:**

1. AWS Console → **API Gateway**
2. Select your API (`love-behavior-translator-api`)
3. Click **Stages** in the left sidebar
4. Click on your stage (usually `prod` or `default`)
5. You'll see **Invoke URL** at the top (e.g., `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod`)
6. **Copy this URL** - this is your API Gateway endpoint URL

**Add to Amplify:**

1. **Environment variables** section
2. Add:
   - **Key**: `VITE_API_BASE_URL`
   - **Value**: Your API Gateway Invoke URL (including `/prod`)
     - Example: `https://abc123xyz.execute-api.us-east-1.amazonaws.com/prod`
3. Click **Next** → **Save and deploy**

### 8.5 Wait for Deployment

Amplify will:
- Clone your repo
- Install dependencies
- Build the React app
- Deploy to hosting

When complete, you'll see a **live URL** (e.g., `https://main.xxxx.amplifyapp.com`)

### 8.6 Test the Application

1. Open the Amplify URL
2. Fill out the behavior analysis form
3. Submit and verify you get AI analysis results

---

## Step 9: Route 53 + Custom Domain (Optional)

### 9.1 Register or Use Existing Domain

#### Option A: Register Domain in Route 53

1. AWS Console → **Route 53** → **Registered domains** → **Register domain**
2. Search for your desired domain
3. Add to cart and complete purchase
4. Route 53 will automatically create a hosted zone

#### Option B: Use Existing Domain (External Registrar)

1. Route 53 → **Hosted zones** → **Create hosted zone**
2. **Domain name**: Your domain (e.g., `lovebehaviortranslator.com`)
3. **Type**: **Public hosted zone**
4. Click **Create hosted zone**
5. Copy the **NS (name server)** records
6. Go to your domain registrar (GoDaddy, Namecheap, etc.)
7. Replace the domain's nameservers with the Route 53 NS values

> **DNS Propagation**: Changes can take 5 minutes to 48 hours, but usually complete within an hour.

### 9.2 Add Custom Domain to Amplify

1. AWS Amplify → Your app → **Domain management**
2. Click **Add domain**
3. Enter your domain (e.g., `lovebehaviortranslator.com`)
4. **Branch**: Select `main`
5. **Subdomains**:
   - `lovebehaviortranslator.com` → `main`
   - `www.lovebehaviortranslator.com` → `main`
6. Click **Configure domain**

Amplify will:
- Automatically create Route 53 records (if domain is in Route 53), OR
- Show you DNS records to add manually (if DNS is elsewhere)

7. Wait for **Domain activation** (SSL certificate provisioning, usually 5-15 minutes)

Your frontend is now live at your custom domain!

### 9.3 (Optional) Add Custom Domain to API Gateway

This gives you a stable API URL like `api.lovebehaviortranslator.com`.

#### Step 1: Request ACM Certificate

1. AWS Console → **AWS Certificate Manager (ACM)** (same region as API Gateway)
2. **Request a certificate** → **Request a public certificate**
3. **Domain name**: `api.lovebehaviortranslator.com`
4. **Validation method**: **DNS validation**
5. Click **Request**
6. In ACM, expand the certificate → **Create record in Route 53** (or add manually to your DNS)
7. Wait until certificate status is **Issued**

#### Step 2: Create API Gateway Custom Domain

1. API Gateway → **Custom domain names** → **Create**
2. **Domain name**: `api.lovebehaviortranslator.com`
3. **Endpoint type**: **Regional**
4. **ACM certificate**: Select your issued certificate
5. Click **Create domain name**

#### Step 3: Map API to Custom Domain

1. In custom domain details → **API mappings** → **Create**
2. **API**: `love-behavior-translator-api`
3. **Stage**: `prod`
4. **Path**: Leave blank (so base URL is `https://api.lovebehaviortranslator.com`)
5. Click **Save**

#### Step 4: Create Route 53 Record

1. Route 53 → **Hosted zones** → Your domain
2. **Create record**
3. **Record name**: `api`
4. **Record type**: **A - Routes traffic to an IPv4 address**
5. ✅ **Alias**: Turn ON
6. **Alias target**: Select the API Gateway regional domain (shown in custom domain details)
7. Click **Create records**

#### Step 5: Update Amplify Environment Variable

1. Amplify → Your app → **Environment variables**
2. Edit `VITE_API_BASE_URL`:
   - Change to: `https://api.lovebehaviortranslator.com`
3. **Save** → This will trigger a new build

Your API is now accessible at `https://api.lovebehaviortranslator.com/analyze`

---

## Step 10: Security Hardening (Recommended)

After everything works, tighten security:

### CORS Configuration

1. API Gateway → Your API → Resources
2. For each resource, **Enable CORS** again
3. **Access-Control-Allow-Origin**: Replace `*` with your Amplify domain
   - Example: `https://lovebehaviortranslator.com`

### IAM Policy Tightening

1. IAM → Roles → `LoveBehaviorTranslatorLambdaRole`
2. Edit the inline policy
3. Replace `Resource: "*"` with specific ARNs:
   - DynamoDB: `arn:aws:dynamodb:<region>:<account>:table/LoveBehaviorTranslator`
   - S3: `arn:aws:s3:::your-bucket-name` and `arn:aws:s3:::your-bucket-name/*`
   - Secrets Manager: Your secret ARN

### Additional Security (Optional)

- **WAF**: Add AWS WAF to API Gateway to protect against abuse
- **SES**: Request production access to send emails to any address
- **CloudWatch Alarms**: Set up alerts for Lambda errors or high API usage

---

## API Reference

### POST `/analyze`

Analyze relationship behavior and get AI-powered insights.

**Request Body:**

```json
{
  "behavior_description": "My partner has been canceling plans last minute...",
  "relationship_type": "dating",
  "relationship_length": "6 months",
  "emotional_state": "anxious",
  "analysis_mode": "gentle",
  "email_to": "user@example.com"
}
```

**Response:**

```json
{
  "analysis": "This behavior might indicate...",
  "emotional_insight": "Your partner may be feeling...",
  "practical_advice": "Consider having an open conversation...",
  "reassurance": "Your feelings are valid..."
}
```

**Analysis Modes:**
- `gentle` - Warm, empathetic tone
- `analytical` - Structured, logical approach
- `brutally_honest` - Direct truth-telling
- `light_funny` - Lighthearted, humorous tone

### GET `/health`

Health check endpoint.

**Response:**

```json
{
  "status": "healthy"
}
```

---

## Troubleshooting

### Amplify Shows "Welcome" Page (App Not Deployed)

If you see the Amplify welcome page instead of your app:

1. **Check Build Status**:
   - AWS Amplify Console → Your app → **Build history**
   - Look for failed builds (red X) or in-progress builds
   - Click on a build to see detailed logs

2. **Common Build Issues**:

   **Issue: "npm ci" fails**
   - Ensure `package.json` exists in `frontend/` directory
   - Check that all dependencies are valid
   - Look for version conflicts in build logs

   **Issue: "Build output not found"**
   - Verify `amplify.yml` has correct `baseDirectory: frontend/dist`
   - Ensure Vite build produces `index.html` in `frontend/dist/`
   - Check build logs for actual output directory

   **Issue: Environment variable not set**
   - Amplify → App settings → **Environment variables**
   - Verify `VITE_API_BASE_URL` is set correctly
   - Must include full URL with `/prod` (e.g., `https://xxxx.execute-api.us-east-1.amazonaws.com/prod`)

3. **Test Build Locally**:
   ```powershell
   cd frontend
   npm install
   npm run build
   ```
   - Check that `frontend/dist/index.html` exists after build
   - If local build fails, fix those errors first

4. **Redeploy**:
   - Amplify → **Redeploy this version** (if build succeeded but app doesn't show)
   - Or trigger a new build by pushing a commit

### Lambda Function Errors

- Check **CloudWatch Logs** in Lambda console
- Verify all environment variables are set correctly
- Ensure IAM role has correct permissions

### API Gateway 500 Errors

- Check Lambda function logs
- Verify Lambda handler is correct
- Test Lambda function directly in console

### CORS Errors in Browser

- Verify CORS is enabled on API Gateway resources
- Check that `VITE_API_BASE_URL` matches your actual API URL
- Ensure allowed origins include your Amplify domain
- Check browser console for specific CORS error messages

---

## Next Steps

- Customize the AI prompts in `backend/src/LoveBehaviorTranslator.Function/PromptFactory.cs`
- Add more analysis modes or features
- Set up monitoring and alerts in CloudWatch
- Configure custom error pages in Amplify

---

**Need Help?** Check the main `README.md` for architecture details and code structure.
