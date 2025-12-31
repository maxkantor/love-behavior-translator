# Fix "Invalid or expired verification code" Error

The 400 error means the endpoint is working, but the code verification is failing. Here's how to fix it:

## Check 1: Verify DynamoDB Tables Exist

1. Go to **AWS DynamoDB Console**
2. Check if these tables exist with **exact names**:
   - `LoveBehaviorTranslatorEmailVerification` ✅
   - `LoveBehaviorTranslatorEmailLinks` ✅

**Important:** Table names are case-sensitive and must match exactly!

## Check 2: Verify Lambda Has DynamoDB Permissions

1. Go to **AWS Lambda Console**
2. Find: `LoveBehaviorTranslatorFunction` (your original function)
3. Go to **Configuration** → **Permissions**
4. Click on the **Execution role** name
5. In the **Permissions** tab, check if there's a policy that allows:
   - `dynamodb:GetItem`
   - `dynamodb:PutItem`
   - `dynamodb:DeleteItem`
   - On resources: `arn:aws:dynamodb:*:*:table/LoveBehaviorTranslatorEmailVerification`
   - On resources: `arn:aws:dynamodb:*:*:table/LoveBehaviorTranslatorEmailLinks`

If missing, add these permissions.

## Check 3: Check CloudWatch Logs

1. Go to **AWS CloudWatch Console**
2. Click **Log groups**
3. Find: `/aws/lambda/LoveBehaviorTranslatorFunction`
4. Click the latest log stream
5. Look for errors when you:
   - Send verification code (should see "Generated verification code for...")
   - Verify code (should see "No verification code found" or "Invalid verification code")

Common errors:
- `ResourceNotFoundException` = Table doesn't exist
- `AccessDeniedException` = Lambda doesn't have permissions
- `No verification code found` = Code was never stored or expired

## Check 4: Verify Code Was Sent Successfully

When you click "Send Verification Code", check CloudWatch logs to see if:
- The code was generated
- The code was stored in DynamoDB
- The email was sent via SES

If you see errors in the logs, that's the root cause.

## Quick Fix: Add DynamoDB Permissions to Lambda

If Lambda is missing permissions, add this policy to the execution role:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:DeleteItem",
        "dynamodb:Query",
        "dynamodb:Scan"
      ],
      "Resource": [
        "arn:aws:dynamodb:*:*:table/LoveBehaviorTranslatorEmailVerification",
        "arn:aws:dynamodb:*:*:table/LoveBehaviorTranslatorEmailLinks"
      ]
    }
  ]
}
```

## Test Flow

1. Send verification code → Check CloudWatch logs for success
2. Check DynamoDB table → Should see the code stored
3. Verify code → Should match and work

If step 1 fails, the code is never stored, so verification will always fail.
