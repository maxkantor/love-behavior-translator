# Fix: Use Original Lambda Function

You have two Lambda functions. We need to:
1. Update the **original** Lambda with latest code
2. Configure the **original** API Gateway to use the original Lambda
3. Add email verification endpoints to the original API Gateway

## Step 1: Update Original Lambda Function

1. Go to **AWS Lambda Console**
2. Find: `LoveBehaviorTranslatorFunction` (your original function)
3. Go to **Code** tab
4. Click **Upload from** → **.zip file**
5. Upload: `C:\Apps\LoveBehaviorTranslator\backend\dist\function.zip`
6. Click **Save**

## Step 2: Verify Environment Variables in Original Lambda

Make sure the original Lambda has:
- `SES_FROM_EMAIL`: `support@lovebehaviortranslator.com` ✅ (you already have this)
- All other variables from your original setup

## Step 3: Add Email Endpoints to Original API Gateway

Your original API Gateway is: `8dr22prv81.execute-api.us-east-1.amazonaws.com`

### Add `/email/send-verification` endpoint:

1. Go to **API Gateway Console**
2. Select your **original API** (love-behavior-translator-api)
3. Go to **Resources**
4. Click **Create resource**:
   - **Resource Name**: `email`
   - **Resource Path**: `email`
   - Check **"Enable API Gateway CORS"**
   - Click **Create resource**

5. With `/email` selected, click **Create resource** again:
   - **Resource Name**: `send-verification`
   - **Resource Path**: `send-verification`
   - Check **"Enable API Gateway CORS"**
   - Click **Create resource**

6. Select `/email/send-verification`, click **Create method**:
   - Choose **POST**
   - Integration type: **Lambda Function**
   - Check **"Use Lambda Proxy integration"**
   - **Lambda Function**: Select `LoveBehaviorTranslatorFunction` (your original function)
   - Click **Save** and confirm

7. Click **Actions** → **Enable CORS**:
   - **Access-Control-Allow-Headers**: `Content-Type,Authorization,x-user-id`
   - Click **Enable CORS and replace existing CORS headers**

### Add `/email/verify` endpoint:

1. Select `/email` resource
2. Click **Create resource**:
   - **Resource Name**: `verify`
   - **Resource Path**: `verify`
   - Check **"Enable API Gateway CORS"**
   - Click **Create resource**

3. Select `/email/verify`, click **Create method**:
   - Choose **POST**
   - Integration type: **Lambda Function**
   - Check **"Use Lambda Proxy integration"**
   - **Lambda Function**: Select `LoveBehaviorTranslatorFunction` (your original function)
   - Click **Save** and confirm

4. Click **Actions** → **Enable CORS** (same settings as above)

### Add `/credits/restore` endpoint:

1. Select `/credits` resource (should already exist)
2. Click **Create resource**:
   - **Resource Name**: `restore`
   - **Resource Path**: `restore`
   - Check **"Enable API Gateway CORS"**
   - Click **Create resource**

3. Select `/credits/restore`, click **Create method**:
   - Choose **POST**
   - Integration type: **Lambda Function**
   - Check **"Use Lambda Proxy integration"**
   - **Lambda Function**: Select `LoveBehaviorTranslatorFunction` (your original function)
   - Click **Save** and confirm

4. Click **Actions** → **Enable CORS** (same settings as above)

## Step 4: Deploy Original API Gateway

1. In API Gateway Console, with your original API selected
2. Click **Actions** → **Deploy API**
3. Select **"prod"** stage (or your stage name)
4. Click **Deploy**

## Step 5: Verify Frontend Uses Original API

Make sure your frontend `VITE_API_BASE_URL` points to:
```
https://8dr22prv81.execute-api.us-east-1.amazonaws.com/prod
```

(Not the new CDK-created API Gateway)

## Step 6: Test

Try the "Restore Credits" feature - it should now work with your original Lambda and API Gateway!

## Optional: Clean Up CDK Resources

After everything works, you can optionally delete the CDK-created resources:
- The new Lambda function (LoveBehaviorTranslatorSta-LoveBehaviorTranslatorFu-...)
- The new API Gateway (as0t8t7mj7)

But keep the CDK stack code for future reference.
