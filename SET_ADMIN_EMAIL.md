# Set Admin Email for Notifications

Contact form submissions and payment notifications are now sent to your admin email address instead of the support email.

## Step 1: Add ADMIN_EMAIL Environment Variable

1. **Go to AWS Lambda Console:**
   - AWS Console → Lambda → Functions
   - Click on `LoveBehaviorTranslatorFunction`

2. **Add Environment Variable:**
   - Go to **Configuration** → **Environment variables**
   - Click **Edit**
   - Click **Add environment variable**
   - **Key:** `ADMIN_EMAIL`
   - **Value:** `mykantor@bellsouth.net`
   - Click **Save**

## Step 2: Upload Updated Lambda Function

1. **Upload the new function:**
   - Still in Lambda function page
   - Go to **Code** tab
   - Click **Upload from** → **.zip file**
   - Select `backend/dist/function.zip`
   - Click **Save**

## Step 3: Verify

After uploading, test by:
1. Submit a contact form
2. Check CloudWatch logs - you should see:
   - `📧 Preparing contact notification email: From=support@lovebehaviortranslator.com, To=mykantor@bellsouth.net`
3. Check your email inbox at `mykantor@bellsouth.net`

## Important Notes

- **If `ADMIN_EMAIL` is not set**, it will fall back to `SES_FROM_EMAIL` (support@lovebehaviortranslator.com)
- **The sender email** (`SES_FROM_EMAIL`) must still be verified in SES
- **The admin email** (`ADMIN_EMAIL`) must be verified in SES if you're in sandbox mode
- **Both contact form and payment notifications** will now go to the admin email

## Current Configuration

- **SES_FROM_EMAIL:** `support@lovebehaviortranslator.com` (sender)
- **ADMIN_EMAIL:** `mykantor@bellsouth.net` (recipient - needs to be set)

---

**After setting `ADMIN_EMAIL`, all contact form submissions and payment notifications will be sent to your admin email!**

