# Troubleshooting Contact Form Emails

If contact form submissions are not sending emails, follow these steps:

## Step 1: Verify Lambda Function is Updated

1. **Check if the latest code is deployed:**
   - Go to AWS Lambda Console
   - Find `LoveBehaviorTranslatorFunction`
   - Check the "Last modified" date
   - If it's old, upload the latest `backend/dist/function.zip`

2. **Upload the latest function:**
   ```powershell
   # Build the function
   cd backend
   .\build.ps1
   
   # Then upload backend/dist/function.zip to Lambda
   ```

## Step 2: Check CloudWatch Logs

1. **Go to CloudWatch Logs:**
   - AWS Console → CloudWatch → Log groups
   - Find `/aws/lambda/LoveBehaviorTranslatorFunction`
   - Click on the latest log stream

2. **Submit a test contact form** and immediately check the logs

3. **Look for these log messages:**
   - `📝 Storing contact form submission` - Contact form was received
   - `✅ Contact stored in DynamoDB` - Data saved successfully
   - `📧 SendContactNotification called` - Email function was called
   - `📧 Preparing contact notification email` - Email is being prepared
   - `✅ Contact notification email sent successfully` - Email was sent
   - `❌ SES_FROM_EMAIL not set` - Environment variable missing
   - `❌ Contact notification: SES MessageRejectedException` - SES rejected the email

## Step 3: Verify Environment Variables

1. **Check Lambda Environment Variables:**
   - AWS Lambda Console → Your function → Configuration → Environment variables
   - Verify `SES_FROM_EMAIL` is set
   - Value should be: `support@lovebehaviortranslator.com` (or your verified email)

2. **If missing, add it:**
   - Click "Edit"
   - Add: `SES_FROM_EMAIL` = `support@lovebehaviortranslator.com`
   - Click "Save"

## Step 4: Verify SES Configuration

1. **Check if email is verified in SES:**
   - AWS Console → SES → Verified identities
   - Verify `support@lovebehaviortranslator.com` is listed and verified
   - Status should be "Verified"

2. **Check SES Sandbox Mode:**
   - AWS Console → SES → Account dashboard
   - If in "Sandbox mode":
     - You can only send TO verified email addresses
     - The recipient (`SES_FROM_EMAIL`) must be verified
   - To send to any email, request production access

3. **Verify SES Region:**
   - Make sure SES is in the same region as your Lambda function
   - Check Lambda function region (usually in function ARN)
   - Check SES region in SES console

## Step 5: Check IAM Permissions

1. **Verify Lambda execution role has SES permissions:**
   - AWS Lambda Console → Your function → Configuration → Permissions
   - Click on the execution role name
   - Check if it has `ses:SendEmail` permission
   - If not, add this policy:
     ```json
     {
       "Version": "2012-10-17",
       "Statement": [
         {
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

## Step 6: Test Email Sending Directly

1. **Use the test email endpoint (if logged in as admin):**
   - POST to `/admin/test-email`
   - Requires admin authentication
   - This will test if SES email sending works at all

2. **Check CloudWatch logs after test:**
   - Look for `📧 Test email:` log messages
   - Check for any errors

## Common Issues and Solutions

### Issue: "SES_FROM_EMAIL not set"
**Solution:** Add the environment variable in Lambda configuration

### Issue: "Email address not verified"
**Solution:** 
- Go to SES → Verified identities
- Verify `support@lovebehaviortranslator.com`
- Check your email and click the verification link

### Issue: "MessageRejectedException"
**Possible causes:**
- Email address not verified in SES
- SES in sandbox mode and recipient not verified
- Invalid email format
- SES account suspended

**Solution:**
- Verify the sender email in SES
- If in sandbox, verify the recipient email too
- Check SES account status

### Issue: No logs appearing
**Solution:**
- Make sure you're checking the correct log group
- Wait a few seconds after submitting the form
- Check if the contact form submission is reaching Lambda (check API Gateway logs)

### Issue: Contact form works but no email
**Solution:**
- Check CloudWatch logs for email-specific errors
- Verify `SES_FROM_EMAIL` is set correctly
- Check if SES is in the same region as Lambda
- Verify IAM permissions

## Quick Checklist

- [ ] Latest Lambda function uploaded (`backend/dist/function.zip`)
- [ ] `SES_FROM_EMAIL` environment variable set in Lambda
- [ ] Email address verified in SES
- [ ] SES and Lambda in same AWS region
- [ ] Lambda execution role has `ses:SendEmail` permission
- [ ] Checked CloudWatch logs for errors
- [ ] Tested with admin test email endpoint

## Still Not Working?

1. **Check CloudWatch logs** - They will show exactly what's happening
2. **Test SES directly** - Try sending an email from SES console to verify SES works
3. **Check API Gateway** - Make sure `/contact` endpoint is correctly configured
4. **Verify the contact form** - Check browser console for any frontend errors

---

**Note:** Contact form submissions are stored in DynamoDB even if email fails. Check the admin dashboard to see if contacts are being saved.

