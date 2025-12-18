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

## Step 4: (Optional) Configure SES for Email

Only needed if you want the "email me this analysis" feature.

1. AWS Console → **Amazon SES** (same region as Lambda)
2. **Identities** → **Create identity**
3. Choose:
   - **Email address** (fastest for testing)
   - **Domain** (better for production)
4. Follow verification steps (check email or add DNS records)
5. **Note the verified sender email** — you'll use this as Lambda env var `SES_FROM_EMAIL`

> **SES Sandbox**: If your account is in SES Sandbox, you can only email **verified recipient addresses** until you request production access.

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

### 7.3.1 Create `/admin` Proxy Resource (For Admin Routes)

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

For each resource (`/`, `/health`, `/analyze`, `/admin/*`):

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

### 7.5 Deploy API

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

1. **Environment variables** section
2. Add:
   - **Key**: `VITE_API_BASE_URL`
   - **Value**: Your API Gateway Invoke URL (including `/prod`)
     - Example: `https://xxxx.execute-api.us-east-1.amazonaws.com/prod`
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
