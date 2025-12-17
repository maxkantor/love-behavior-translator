# Manual AWS Console Setup (No CDK/CLI)

This guide walks you through setting up **Love Behavior Translator** entirely from the **AWS Console**, using:

- **React frontend** hosted on **AWS Amplify**
- **.NET 8 Lambda** backend
- **API Gateway (REST API / v1)**
- **DynamoDB** (rate limiting + request logs with TTL)
- **Secrets Manager** (OpenAI API key)
- **S3** (analysis artifacts)
- **SES** (optional “email me results”)

All resources should be created in **one AWS region** (pick one and stick to it).

---

## Prerequisites

- An AWS account with permission to create: IAM roles/policies, Lambda, API Gateway, DynamoDB, S3, Secrets Manager, Amplify, SES.
- An **OpenAI API key**
- Local tooling (still required to build the Lambda zip):
  - **.NET 8 SDK**
  - Windows PowerShell (or equivalent)

---

## 0) Build the Lambda deployment zip (local)

Even with console-only AWS setup, you must upload a Lambda zip. Build it from repo root:

```powershell
.\backend\build.ps1
```

This produces:

- `backend\dist\function.zip`

You will upload this file in the Lambda Console later.

### 0.1 Troubleshooting: if you don’t have the zip yet

#### A) Install .NET 8 SDK

- Download and install **.NET 8 SDK** (not just runtime), then reopen your terminal.

Verify:

```powershell
dotnet --info
```

You should see a `.NET SDKs installed` entry for `8.x`.

#### B) Allow running the build script (PowerShell execution policy)

If PowerShell blocks `build.ps1`, run PowerShell **as your user** and do:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Then rerun:

```powershell
.\backend\build.ps1
```

#### C) Manual build commands (if you prefer not to use build.ps1)

From repo root:

```powershell
dotnet restore .\backend\LoveBehaviorTranslator.sln
dotnet publish .\backend\src\LoveBehaviorTranslator.Function\LoveBehaviorTranslator.Function.csproj -c Release -o .\backend\dist\publish
Compress-Archive -Path .\backend\dist\publish\* -DestinationPath .\backend\dist\function.zip -Force
```

After this, confirm the file exists:

```powershell
Test-Path .\backend\dist\function.zip
```

---

## 1) Store the OpenAI API key in Secrets Manager

1. AWS Console → **Secrets Manager**
2. Click **Store a new secret**
3. **Secret type**: “Other type of secret”
4. Store either:
   - **Plaintext**: paste your `sk-...` key, **or**
   - **Key/value JSON**:
     - Key: `OPENAI_API_KEY`
     - Value: `sk-...`
5. **Secret name**: `love-behavior-translator/openai-api-key`
6. Create the secret
7. Open the secret → copy its **ARN** (you’ll use it for Lambda env var `OPENAI_SECRET_ARN`)

---

## 2) Create DynamoDB table (rate limit + logs + TTL)

1. AWS Console → **DynamoDB** → **Tables** → **Create table**
2. Table name: `LoveBehaviorTranslator`
3. Partition key: `pk` (**String**)
4. Sort key: `sk` (**String**)
5. Capacity mode: **On-demand**
6. Create table
7. After creation, open the table → enable **TTL**
   - Find **Time to live (TTL)** (in “Additional settings” or “Table details” depending on console UI)
   - Enable TTL attribute: `ttl`

> This app uses DynamoDB TTL for:
> - per-IP per-minute counters (short-lived)
> - request logs (longer-lived)

---

## 3) Create S3 bucket (analysis artifacts)

1. AWS Console → **S3** → **Create bucket**
2. Bucket name: must be **globally unique** and follow S3 naming rules (no `<` `>` brackets).
   - Valid examples:
     - `love-behavior-translator-artifacts-2025`
     - `love-behavior-translator-artifacts-mycompany`
     - `love-behavior-translator-artifacts-3f9a2c1d`
3. Keep **Block all public access** = ON
4. Encryption: default (**SSE-S3**) is fine
5. Create bucket

Optional retention:

6. Bucket → **Management** → **Lifecycle rules** → create rule to expire objects after **30 days**

---

## 4) (Optional) Configure SES for “email me this analysis”

If you will use `email_to`, you must set up SES.

1. AWS Console → **Amazon SES**
2. Confirm you’re in the same region as your Lambda
3. **Identities** → **Create identity**
4. Choose:
   - **Email address** (fastest) or
   - **Domain** (best long-term)
5. Verify the identity (follow the emailed link / DNS steps)
6. Note the verified sender email address — you’ll set it as Lambda env var `SES_FROM_EMAIL`

SES sandbox note:

- If your account is in **SES Sandbox**, you can only email **verified recipient addresses** until you request production access.

---

## 5) Create an IAM role for the Lambda function

### 5.1 Create the role

1. AWS Console → **IAM** → **Roles** → **Create role**
2. Trusted entity: **AWS service**
3. Use case: **Lambda**
4. Attach managed policy:
   - `AWSLambdaBasicExecutionRole`
5. Role name: `LoveBehaviorTranslatorLambdaRole`
6. Create role

### 5.2 Add inline permissions (DynamoDB + S3 + Secrets + SES)

1. IAM → Roles → open `LoveBehaviorTranslatorLambdaRole`
2. **Add permissions** → **Create inline policy**
3. Choose **JSON** tab, paste:

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
        "dynamodb:Query"
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

4. Policy name: `LoveBehaviorTranslatorInline`
5. Create policy

> You can later tighten `Resource` from `*` to specific ARNs for the table, bucket, and secret.

---

## 6) Create the Lambda function and upload the zip

### 6.1 Create function

1. AWS Console → **Lambda** → **Create function**
2. “Author from scratch”
3. Function name: `LoveBehaviorTranslatorFunction`
4. Runtime: **.NET 8**
5. Architecture: `x86_64`
6. Permissions: **Use an existing role** → `LoveBehaviorTranslatorLambdaRole`
7. Create function

### 6.2 Upload deployment package

1. Lambda → `LoveBehaviorTranslatorFunction` → **Code** tab
2. “Upload from” → **.zip file**
3. Upload `backend\dist\function.zip`

### 6.3 Set handler

1. Lambda → **Configuration** → **Runtime settings** → **Edit**
2. Set **Handler**:

`LoveBehaviorTranslator.Function::LoveBehaviorTranslator.Function.Function::FunctionHandler`

3. Save

### 6.4 Configure environment variables

Lambda → **Configuration** → **Environment variables** → **Edit**:

- `TABLE_NAME` = `LoveBehaviorTranslator`
- `ARTIFACTS_BUCKET` = your S3 bucket name
- `OPENAI_SECRET_ARN` = the secret ARN from Secrets Manager
- `OPENAI_MODEL` = `gpt-4.1-mini` (or your preferred model)
- `RATE_LIMIT_PER_MINUTE` = `10`
- `RATE_LIMIT_BURST` = `5`
- `SES_FROM_EMAIL` = your verified SES email (optional; leave empty if not using email)

Save.

### 6.5 Configure timeout/memory

Lambda → **Configuration** → **General configuration** → **Edit**:

- Memory: **1024 MB**
- Timeout: **29 seconds**

Save.

---

## 7) Create API Gateway REST API (v1) and integrate Lambda

1. AWS Console → **API Gateway**
2. **Create API** → **REST API** (NOT HTTP API)
3. “New API”
4. API name: `love-behavior-translator-api`
5. Create API

### 7.1 Create `/health` GET

1. Resources → **Create resource**
2. Resource name: `health`, path: `/health`
3. Create
4. Select `/health` → **Create method** → `GET`
5. Integration type: **Lambda Function**
6. Enable **Lambda Proxy integration**
7. Select Lambda: `LoveBehaviorTranslatorFunction`
8. Save

### 7.2 Create `/analyze` POST

1. Resources → **Create resource**
2. Resource name: `analyze`, path: `/analyze`
3. Create
4. Select `/analyze` → **Create method** → `POST`
5. Integration: Lambda Proxy → `LoveBehaviorTranslatorFunction`
6. Save

### 7.3 Enable CORS

For root and each resource (`/health`, `/analyze`):

1. Select the resource in the left pane
2. Click **Enable CORS**
3. Set:
   - Allow Origins: `*` (tighten later to Amplify domain)
   - Allow Methods: `GET,POST,OPTIONS`
   - Allow Headers: `Content-Type,Authorization`
4. Confirm

### 7.4 Deploy to a stage

1. Actions → **Deploy API**
2. Stage: **New stage**
3. Stage name: `prod`
4. Deploy

Copy the **Invoke URL** for stage `prod`, e.g.:

`https://xxxx.execute-api.<region>.amazonaws.com/prod`

Quick test:

- `GET {InvokeUrl}/health` should return `{"status":"healthy"}`

---

## 8) Deploy frontend with AWS Amplify (Console)

### 8.1 Push your code to GitHub (required before Amplify)

Amplify pulls your code from a Git provider (GitHub). Make sure this repo is committed and pushed.

From repo root:

```powershell
git status
git add -A
git commit -m "Initial AWS serverless app (Amplify + Lambda + API Gateway)"
git push
```

If this folder is not a git repo yet:

```powershell
git init
git branch -M main
git add -A
git commit -m "Initial commit"
git remote add origin <YOUR_GITHUB_REPO_URL>
git push -u origin main
```

### 8.2 Connect Amplify Hosting

1. AWS Console → **AWS Amplify** → **Host web app**
2. Connect GitHub and select this repo/branch (`main`)
3. Amplify should detect `amplify.yml`
4. Add environment variable:
   - `VITE_API_BASE_URL` = your API Gateway Invoke URL (including `/prod`)
5. Deploy

After deploy, open the Amplify URL and submit an analysis.

---

## 9) Route 53 + domain registration + custom domains

You can put your app on a real domain using Route 53. There are two common patterns:

- **Recommended**: custom domain for **Amplify** only (e.g. `lovebehaviortranslator.com` and `www.lovebehaviortranslator.com`)
- **Optional**: custom domain for **API Gateway** too (e.g. `api.lovebehaviortranslator.com`)

### 9.1 Register a domain (Route 53) OR use an existing registrar

#### Option A: Register in Route 53

1. AWS Console → **Route 53**
2. **Registered domains** → **Register domain**
3. Search and buy your domain
4. Complete contact details and purchase

Route 53 will create (or you will create) a **Hosted zone** for it.

#### Option B: Domain already registered elsewhere (GoDaddy, Namecheap, etc.)

You can keep your registrar and just point DNS to Route 53:

1. Route 53 → **Hosted zones** → **Create hosted zone**
2. Domain name: your domain (e.g. `lovebehaviortranslator.com`)
3. Type: **Public hosted zone**
4. Create hosted zone
5. In the hosted zone, copy the **NS (name server)** values
6. Go to your registrar and replace the domain’s nameservers with the Route 53 NS values

> DNS changes can take some time to propagate (often minutes, sometimes longer).

### 9.2 Add a custom domain to Amplify (frontend)

1. AWS Console → **AWS Amplify** → your app → **Domain management**
2. Click **Add domain**
3. Enter your domain (e.g. `lovebehaviortranslator.com`)
4. Choose which branches to map (usually `main`)
5. Add subdomains:
   - `lovebehaviortranslator.com` → `main`
   - `www.lovebehaviortranslator.com` → `main`
6. Amplify will either:
   - automatically create Route 53 records (if your domain is in Route 53), or
   - show you DNS records to add manually (if your DNS is elsewhere)
7. Wait for verification + SSL issuance to complete

At this point your frontend is served at your custom domain.

### 9.3 (Optional) Add a custom domain to API Gateway (backend)

This gives you a stable API hostname like `api.lovebehaviortranslator.com`.

#### Step 1: Request a certificate (ACM)

1. AWS Console → **AWS Certificate Manager (ACM)** (same region as API Gateway)
2. **Request a certificate** → Public certificate
3. Domain name: `api.lovebehaviortranslator.com`
4. Validation method: **DNS validation**
5. Request
6. In ACM, create the suggested DNS validation record in Route 53 (or your DNS provider)
7. Wait until the certificate status is **Issued**

#### Step 2: Create API Gateway custom domain

1. AWS Console → **API Gateway**
2. Left nav → **Custom domain names** → **Create**
3. Domain name: `api.lovebehaviortranslator.com`
4. Endpoint type: **Regional**
5. ACM certificate: select the issued certificate
6. Create domain name

#### Step 3: Map your API stage to the custom domain

1. In the custom domain details → **API mappings** → **Create**
2. API: `love-behavior-translator-api`
3. Stage: `prod`
4. Path: leave blank (recommended) so the base URL becomes:
   - `https://api.lovebehaviortranslator.com/analyze`
   - `https://api.lovebehaviortranslator.com/health`
5. Create mapping

#### Step 4: Create Route 53 record for the API domain

1. Route 53 → **Hosted zones** → your domain
2. **Create record**
3. Record name: `api` (for `api.lovebehaviortranslator.com`)
4. Record type: **A**
5. Turn on **Alias**
6. Alias target: select the API Gateway regional domain shown in “Custom domain names”
7. Create record

#### Step 5: Update Amplify environment variable

If you switch to the API custom domain, update Amplify env var:

- `VITE_API_BASE_URL = https://api.lovebehaviortranslator.com`

Redeploy Amplify (or trigger a new build) so the frontend uses the new API base URL.

---

## 10) Recommended hardening (after it works)

- **CORS**: set allowed origin to your Amplify domain instead of `*`
- **IAM**: replace `Resource: "*"` with exact ARNs:
  - DynamoDB table ARN
  - S3 bucket + `bucket/*`
  - Secrets Manager secret ARN
- **WAF** (optional): protect API Gateway from abuse
- **SES**: move out of sandbox if needed

---

## API request format

**POST** `/analyze` body:

```json
{
  "behavior_description": "My partner has been canceling plans...",
  "relationship_type": "dating",
  "relationship_length": "6 months",
  "emotional_state": "anxious",
  "analysis_mode": "gentle",
  "email_to": "name@example.com"
}
```

Modes supported: `gentle`, `analytical`, `brutally_honest`, `light_funny`.


