# Update API Gateway URL

The new API Gateway has been deployed. Update the API URL in the following places:

## New API Gateway URL
```
https://as0t8t7mj7.execute-api.us-east-1.amazonaws.com/prod
```

## 1. Amplify Console (Production)

1. Go to AWS Amplify Console
2. Select your app
3. Go to **"Environment variables"** in the left sidebar
4. Find or add `VITE_API_BASE_URL`
5. Set the value to: `https://as0t8t7mj7.execute-api.us-east-1.amazonaws.com/prod`
6. Click **"Save"**
7. **Redeploy** your app (or trigger a new build)

## 2. Local Development

A `.env` file has been created in the `frontend/` directory with the new URL.

For local development:
```bash
cd frontend
npm run dev
```

The `.env` file will automatically be loaded by Vite.

## 3. Verify the Update

After updating, test the "Restore Credits" feature:
1. Open the app
2. Click "Restore Credits"
3. Enter your email
4. Click "Send Verification Code"

The CORS error should be resolved and the verification code should be sent.

## Old API Gateway (to be deprecated)
```
https://8dr22prv81.execute-api.us-east-1.amazonaws.com/prod
```

