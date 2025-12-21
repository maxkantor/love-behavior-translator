# Deploy Purchase Activity Tracking

This guide will help you deploy the new purchase activity tracking feature.

## Prerequisites

1. ✅ Code changes are committed and pushed to GitHub
2. ⚠️ Frontend will auto-deploy via Amplify (check Amplify console)
3. ⚠️ Backend Lambda function needs to be uploaded
4. ⚠️ DynamoDB table needs to be created

---

## Step 1: Create DynamoDB Table for Activities

1. **Go to AWS Console → DynamoDB → Tables → Create table**

2. **Table configuration:**
   - **Table name:** `LoveBehaviorTranslatorActivities`
   - **Partition key:** `activityId` (type: **String**)
   - **Table settings:** **On-demand** capacity mode
   - Click **Create table**

3. **Enable TTL (Time to Live):**
   - Open the `LoveBehaviorTranslatorActivities` table
   - Go to **Additional settings** → **Time to live (TTL)**
   - Enable TTL attribute: `ttl`
   - Click **Save**

> **Note:** TTL will automatically delete activities after 2 years

---

## Step 2: Upload Updated Lambda Function

1. **Go to AWS Lambda Console:**
   - AWS Console → Lambda → Functions
   - Click on `LoveBehaviorTranslatorFunction`

2. **Upload the new function:**
   - Go to **Code** tab
   - Click **Upload from** → **.zip file**
   - Select `backend/dist/function.zip` (from your local machine)
   - Click **Save**

3. **Verify upload:**
   - Wait for upload to complete
   - Check that the function code shows the new `StorePurchaseActivity` method

---

## Step 3: Verify ADMIN_EMAIL Environment Variable

1. **In Lambda function page:**
   - Go to **Configuration** → **Environment variables**
   - Verify `ADMIN_EMAIL` is set to `mykantor@bellsouth.net`
   - If not set, add it:
     - Click **Edit**
     - Click **Add environment variable**
     - **Key:** `ADMIN_EMAIL`
     - **Value:** `mykantor@bellsouth.net`
     - Click **Save**

---

## Step 4: Verify Frontend Deployment

1. **Check Amplify Console:**
   - AWS Console → Amplify → Your app
   - Check **Build history** - latest build should be from your recent commit
   - Wait for build to complete (usually 2-5 minutes)

2. **Hard refresh your browser:**
   - Press `Ctrl+F5` (Windows) or `Cmd+Shift+R` (Mac)
   - This clears cache and loads the new frontend code

---

## Step 5: Test the Feature

1. **Make a test purchase:**
   - Go to your app
   - Purchase credits (or use Stripe test mode)
   - Complete the checkout

2. **Check admin dashboard:**
   - Go to admin dashboard
   - Click **"Show Activities"** button in Quick Actions
   - You should see the purchase activity with:
     - Customer name
     - Email
     - Date
     - Amount
     - Credits
     - Card last 4 digits (if available)

3. **Check email:**
   - Check `mykantor@bellsouth.net` inbox
   - You should receive an email notification with all purchase details

4. **Check CloudWatch logs:**
   - AWS Console → CloudWatch → Log groups
   - Find `/aws/lambda/LoveBehaviorTranslatorFunction`
   - Look for logs like:
     - `✅ Stored purchase activity: PURCHASE#...`
     - `✅ Admin notification sent for credit purchase`

---

## Troubleshooting

### "Show Activities" button doesn't show anything

**Possible causes:**
1. Frontend not deployed - Check Amplify build status
2. No activities yet - Make a test purchase
3. DynamoDB table doesn't exist - Create `LoveBehaviorTranslatorActivities` table
4. Lambda function not updated - Upload `backend/dist/function.zip`

**Fix:**
- Check browser console for errors (F12 → Console)
- Check Network tab for `/admin/activities` request
- Verify DynamoDB table exists and has correct structure

### Activities not being stored

**Check CloudWatch logs:**
- Look for errors like: `Failed to store purchase activity`
- Common issues:
  - DynamoDB table doesn't exist
  - Lambda doesn't have DynamoDB write permissions
  - Table name mismatch

**Fix:**
- Verify table name is exactly `LoveBehaviorTranslatorActivities`
- Check Lambda IAM role has DynamoDB permissions
- Verify table has `activityId` as partition key

### Email notifications not working

**Check:**
- `ADMIN_EMAIL` environment variable is set
- Email address is verified in SES
- Check CloudWatch logs for email sending errors

---

## What's New

✅ **Purchase Activity Tracking:**
- All purchases are stored in DynamoDB with full details
- Includes: name, email, date, amount, credits, card last 4

✅ **Enhanced Email Notifications:**
- Admin receives detailed email for each purchase
- Includes all user information and payment details

✅ **Admin Dashboard:**
- "Show Activities" button displays all purchase activities
- Shows customer name, email, date, amount, credits, card info

---

## Next Steps

After deployment:
1. Test with a real purchase
2. Verify activities appear in admin dashboard
3. Verify email notifications are received
4. Monitor CloudWatch logs for any errors

