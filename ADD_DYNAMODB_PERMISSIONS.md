# Add DynamoDB Permissions to Lambda Role

## The Problem

The Lambda execution role `LoveBehaviorTranslatorLambdaRole` is missing DynamoDB permissions for the email verification tables.

**Error from logs:**
```
User: arn:aws:sts::718522948657:assumed-role/LoveBehaviorTranslatorLambdaRole/LoveBehaviorTranslatorFunction 
is not authorized to perform: dynamodb:DeleteItem on resource: 
arn:aws:dynamodb:us-east-1:718522948657:table/LoveBehaviorTranslatorEmailVerification
```

## Solution: Add IAM Policy

### Step 1: Go to IAM Console

1. Go to **AWS IAM Console**
2. Click **Roles** in the left sidebar
3. Find and click: `LoveBehaviorTranslatorLambdaRole`

### Step 2: Add DynamoDB Permissions

1. Click **Add permissions** → **Create inline policy**
2. Click **JSON** tab
3. Paste this policy:

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
        "arn:aws:dynamodb:us-east-1:718522948657:table/LoveBehaviorTranslatorEmailVerification",
        "arn:aws:dynamodb:us-east-1:718522948657:table/LoveBehaviorTranslatorEmailVerification/*",
        "arn:aws:dynamodb:us-east-1:718522948657:table/LoveBehaviorTranslatorEmailLinks",
        "arn:aws:dynamodb:us-east-1:718522948657:table/LoveBehaviorTranslatorEmailLinks/*"
      ]
    }
  ]
}
```

4. Click **Next**
5. **Policy name**: `EmailVerificationDynamoDBAccess`
6. Click **Create policy**

### Step 3: Verify Permissions

The role should now have permissions for:
- ✅ `dynamodb:GetItem` - Read verification codes
- ✅ `dynamodb:PutItem` - Store verification codes and email links
- ✅ `dynamodb:DeleteItem` - Delete used verification codes
- ✅ `dynamodb:Query` and `dynamodb:Scan` - Query operations

### Step 4: Test Again

After adding the permissions, try the "Restore Credits" feature again. It should work now!

## Alternative: Update Existing Policy

If the role already has a DynamoDB policy, you can edit it to add the new tables:

1. Go to the role
2. Find the existing DynamoDB policy
3. Click **Edit**
4. Add the two new table ARNs to the `Resource` array
5. Add `dynamodb:DeleteItem` to the `Action` array if missing
6. Save
