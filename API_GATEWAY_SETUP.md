# API Gateway Configuration Guide

## Option 1: Update CDK Stack (Recommended)

The CDK stack has been updated to include all the new endpoints. Deploy it:

```bash
cd infra
npm install  # if needed
cdk deploy
```

This will automatically:
- Add `/email/send-verification` endpoint
- Add `/email/verify` endpoint  
- Add `/credits/restore` endpoint
- Update CORS headers to include `x-user-id`
- Configure all endpoints with proper Lambda integration

## Option 2: Manual Configuration in API Gateway Console

If you prefer to configure manually or need a quick fix:

### Step 1: Add Email Verification Endpoints

1. Go to API Gateway Console → Your API → Resources
2. Click **"Create resource"**
3. Configure:
   - **Resource Name**: `email`
   - **Resource Path**: `email`
   - Check **"Enable API Gateway CORS"**
   - Click **"Create resource"**

4. With `/email` selected, click **"Create resource"** again:
   - **Resource Name**: `send-verification`
   - **Resource Path**: `send-verification`
   - Check **"Enable API Gateway CORS"**
   - Click **"Create resource"**

5. Select `/email/send-verification`, click **"Create method"**:
   - Choose **POST**
   - Integration type: **Lambda Function**
   - Check **"Use Lambda Proxy integration"**
   - Select your Lambda function
   - Click **"Save"** and confirm

6. Repeat for `/email/verify`:
   - Create resource `verify` under `/email`
   - Add POST method with Lambda proxy integration

### Step 2: Add Restore Credits Endpoint

1. Select `/credits` resource (or create it if it doesn't exist)
2. Click **"Create resource"**:
   - **Resource Name**: `restore`
   - **Resource Path**: `restore`
   - Check **"Enable API Gateway CORS"**
   - Click **"Create resource"**

3. Select `/credits/restore`, click **"Create method"**:
   - Choose **POST**
   - Integration type: **Lambda Function**
   - Check **"Use Lambda Proxy integration"**
   - Select your Lambda function
   - Click **"Save"**

### Step 3: Configure CORS for All New Endpoints

For each new endpoint (`/email/send-verification`, `/email/verify`, `/credits/restore`):

1. Select the resource
2. Click **"Actions"** → **"Enable CORS"**
3. Configure:
   - **Access-Control-Allow-Origin**: `*` (or your domain)
   - **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id`
   - **Access-Control-Allow-Methods**: `POST,OPTIONS`
   - Click **"Enable CORS and replace existing CORS headers"**

### Step 4: Ensure OPTIONS Methods Exist

For each endpoint, verify OPTIONS method exists:
- If missing, click **"Create method"** → **OPTIONS**
- Integration type: **Mock**
- Integration Response: Return 200 with CORS headers
- Or use **"Enable CORS"** which automatically creates OPTIONS

### Step 5: Deploy API

1. Click **"Actions"** → **"Deploy API"**
2. Select **"prod"** stage (or your stage)
3. Click **"Deploy"**

## Verification

Test the endpoints:

```bash
# Test send verification code
curl -X POST https://YOUR_API_URL/prod/email/send-verification \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'

# Should return: {"message":"Verification code sent to your email"}
```

## Troubleshooting

### CORS Errors
- Ensure OPTIONS method exists for each endpoint
- Verify CORS headers include `x-user-id`
- Check that "Enable CORS" was run for each resource

### 404 Errors
- Verify resources are created under correct path
- Check that methods are created (POST, OPTIONS)
- Ensure API is deployed to the correct stage

### Lambda Integration Errors
- Verify Lambda function name matches
- Check "Use Lambda Proxy integration" is enabled
- Verify Lambda has proper IAM permissions

