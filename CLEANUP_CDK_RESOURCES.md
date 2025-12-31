# Clean Up CDK-Created Resources

Since you're using the original Lambda function and API Gateway, you can safely delete the CDK-created resources.

## Resources to Delete

### 1. CDK-Created Lambda Function
- **Name**: `LoveBehaviorTranslatorSta-LoveBehaviorTranslatorFun-kdVjf8mhHQHC`
- **Service**: Lambda
- **Action**: Delete (not needed - you're using the original)

### 2. CDK-Created API Gateway
- **Name**: `LoveBehaviorTranslatorSta-LoveBehaviorTranslatorApi-7OrxFlxJTDYB`
- **Service**: API Gateway
- **Action**: Delete (not needed - you're using the original)

### 3. CDK CloudFormation Stack (Optional)
- **Stack Name**: `LoveBehaviorTranslatorStack`
- **Service**: CloudFormation
- **Action**: Delete (this will remove all CDK-created resources at once)

## How to Delete

### Option 1: Delete Individual Resources

**Delete Lambda Function:**
1. Go to **AWS Lambda Console**
2. Find: `LoveBehaviorTranslatorSta-LoveBehaviorTranslatorFun-...`
3. Click on it → **Actions** → **Delete**
4. Type "delete" to confirm

**Delete API Gateway:**
1. Go to **API Gateway Console**
2. Find: `LoveBehaviorTranslatorSta-LoveBehaviorTranslatorApi-...`
3. Click on it → **Actions** → **Delete**
4. Type the API name to confirm

### Option 2: Delete CDK Stack (Easier)

This will delete all CDK-created resources at once:

```bash
cd infra
npx cdk destroy
```

This will remove:
- The CDK-created Lambda function
- The CDK-created API Gateway
- Any other CDK-created resources

**Note:** This will NOT delete:
- Your original Lambda function (`LoveBehaviorTranslatorFunction`)
- Your original API Gateway (`8dr22prv81`)
- Your DynamoDB tables (they're referenced, not created by CDK)
- Your IAM role (`LoveBehaviorTranslatorLambdaRole`)

## Keep These Resources

✅ **Keep:**
- `LoveBehaviorTranslatorLambdaRole` - Your original Lambda execution role
- Your original Lambda function (`LoveBehaviorTranslatorFunction`)
- Your original API Gateway (`8dr22prv81`)
- All DynamoDB tables
- S3 buckets
- Secrets Manager secrets

## After Cleanup

After deleting the CDK resources, your setup will be:
- ✅ Original Lambda function with latest code
- ✅ Original API Gateway with email verification endpoints
- ✅ Original IAM role with proper permissions
- ✅ All DynamoDB tables intact

Everything should continue working normally!
