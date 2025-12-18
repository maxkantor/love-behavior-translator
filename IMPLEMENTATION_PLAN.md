# Implementation Plan: Enhanced Features

## Overview

This document outlines the implementation plan for:
- Enhanced UI/UX
- User Authentication (AWS Cognito)
- Monetization (Stripe subscriptions)
- Admin Dashboard
- Enhanced Email Features

---

## Phase 1: Enhanced UI/UX ✅ (In Progress)

### Goals
- Modern, polished design
- Smooth animations and transitions
- Better loading states
- Improved mobile responsiveness
- Better error handling and user feedback

### Changes
- Enhanced CSS with animations
- Loading spinners and progress indicators
- Better form validation feedback
- Improved result display with animations
- Mobile-first responsive design

---

## Phase 2: Simple Admin Authentication & User Tracking

### 2.1 Simple Admin Login
- Admin password stored in Secrets Manager
- Simple login page (no Cognito needed)
- JWT token or session-based auth
- Admin-only routes protection

### 2.2 Frontend Admin Auth
- Simple login form
- Store admin session in localStorage
- Protected admin routes
- Logout functionality

### 2.3 Backend Admin Auth
- Admin login endpoint (`POST /admin/login`)
- Verify password against Secrets Manager
- Issue simple JWT or session token
- Admin middleware to protect admin routes

### 2.4 User Tracking (No Login Required)
- Track users by email (for subscriptions)
- DynamoDB table: `Users`
- PK: `email` (user's email address)
- Attributes: subscriptionTier, stripeCustomerId, createdAt, usageCount, etc.
- Users don't need accounts - identified by email for payments

---

## Phase 3: Credit-Based Monetization (Like Pet Behavior Translator)

### 3.1 Credit System Overview
- **1 Credit = 1 Analysis** (simple, clear value)
- Users have a credit balance
- Free tier: 5 credits (configurable)
- Credits can be purchased via Stripe
- Admin can grant/set credits for any user
- Admin has unlimited credits option

### 3.2 Stripe Setup
- Create Stripe account
- Get API keys (store in Secrets Manager)
- Configure webhook endpoint
- Create one-time payment products for credit packs:
  - 10 credits: $4.99
  - 25 credits: $9.99
  - 50 credits: $16.99
  - 100 credits: $29.99
  - 250 credits: $69.99
  - 500 credits: $119.99
- (Configurable pricing - stored in DynamoDB)

### 3.3 Frontend Credit System
- Credit display in header (e.g., "10 Credits")
- Show "1 credit = 1 behavior analysis"
- Credit balance updates after each analysis
- Purchase credits button/page
- Low credit warning when < 3 credits
- Credit purchase flow with Stripe Checkout

### 3.4 Backend Credit Management
- Check credits before processing analysis
- Deduct 1 credit per analysis
- Track credit purchases
- Stripe webhook handler for successful payments
- Grant credits on payment confirmation
- Free tier: 5 credits for new users (configurable)

### 3.5 DynamoDB Updates
- **Users Table**:
  - PK: `userId` (generated or email-based)
  - Attributes: `credits`, `email`, `createdAt`, `lastAnalysisAt`, `totalAnalyses`, `totalPurchases`
- **CreditPurchases Table**:
  - PK: `userId`
  - SK: `purchaseId` (timestamp or UUID)
  - Attributes: `stripePaymentId`, `credits`, `amount`, `purchasedAt`
- **CreditPacks Table** (Configurable pricing):
  - PK: `packId` (e.g., `10`, `25`, `50`, `100`, `250`, `500`)
  - Attributes: `credits`, `price`, `stripePriceId`, `isActive`, `displayOrder`

---

## Phase 4: Admin Dashboard

### 4.1 Admin Authentication
- Admin role in Cognito
- Admin-only routes
- Admin middleware in Lambda

### 4.2 Admin Dashboard UI (Matching Pet Behavior Translator)
- **Set Credits for Any User**
  - User dropdown selector
  - Quick set buttons: 0, 1, 5, 10, 20, 50, 100, 250, 500
  - Custom credit amount input
  - "Set Credits" (replace) and "Grant Credits" (add) buttons
- **Set Your Credits** (Admin personal)
  - Quick buttons: 0, 1, 5, 10, 20, 50, 100, Admin (Unlimited)
- **Quick Actions**
  - Refresh Users (pull from server)
  - Show Activities / Activity Log
  - Reset All activities
- **Dashboard Summary**
  - Today's Translations
  - Today's Purchases
  - Active Tokens (Approx)
  - Free Search Limit (configurable)
- **Configuration**
  - Config Editor (update settings)
- **Override Token Generator**
  - Generate override tokens for testing
- **Support Tickets**
  - View and manage support tickets
- **All Users List**
  - View all users with credits, usage stats

### 4.3 Admin API Endpoints
- `POST /admin/login` - Admin authentication
- `GET /admin/users` - List all users
- `GET /admin/users/:userId` - User details
- `PUT /admin/users/:userId/credits` - Set credits (replace)
- `POST /admin/users/:userId/credits` - Grant credits (add)
- `PUT /admin/me/credits` - Set admin's own credits
- `GET /admin/dashboard` - Dashboard summary (today's stats)
- `GET /admin/activities` - Activity log
- `POST /admin/activities/reset` - Reset all activities
- `GET /admin/config` - Get configuration
- `PUT /admin/config` - Update configuration
- `POST /admin/tokens/override` - Generate override token
- `GET /admin/tickets` - Support tickets
- `GET /admin/analytics` - System analytics

### 4.4 Analytics Features
- Total users
- Active subscriptions
- Revenue metrics
- Usage statistics
- Growth charts

---

## Phase 5: Enhanced Email Features

### 5.1 Email Templates
- HTML email templates
- Branded emails
- Analysis result emails
- Subscription confirmation
- Password reset emails

### 5.2 Email Scheduling
- Queue system for emails
- Retry logic
- Email delivery tracking

### 5.3 Email Types
- Analysis results
- Subscription confirmations
- Payment receipts
- Usage limit warnings
- Admin notifications

---

## Infrastructure Updates

### New AWS Resources Needed
1. **Secrets Manager**
   - `admin-password` - Admin login password (hashed)
   - `admin-jwt-secret` - Secret for signing JWT tokens

2. **New DynamoDB Tables**
   - `Users` - User profiles and subscriptions
   - `UserUsage` - Monthly usage tracking
   - `AdminLogs` - Admin action logs (optional)

3. **New Lambda Functions**
   - `StripeWebhookHandler` - Handle Stripe events
   - `AdminAPI` - Admin endpoints (or extend existing)
   - `UserManagement` - User CRUD operations

4. **Secrets Manager**
   - `stripe-secret-key` - Stripe secret key
   - `stripe-webhook-secret` - Webhook signing secret

5. **API Gateway**
   - New routes for admin and auth
   - Authorizers for protected routes

---

## Implementation Order

1. ✅ **Phase 1: Enhanced UI** (Starting now)
2. **Phase 2: Authentication** (Next)
3. **Phase 3: Monetization** (After auth)
4. **Phase 4: Admin Dashboard** (After monetization)
5. **Phase 5: Enhanced Email** (Final polish)

---

## Dependencies

### Frontend Packages to Add
```json
{
  "@stripe/stripe-js": "^2.0.0",
  "@stripe/react-stripe-js": "^2.0.0",
  "react-router-dom": "^6.20.0",
  "date-fns": "^2.30.0"
}
```

### Backend Packages to Add
```xml
<PackageReference Include="Stripe.net" Version="43.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
```

---

## Testing Strategy

- Unit tests for business logic
- Integration tests for API endpoints
- E2E tests for critical user flows
- Load testing for rate limits

---

## Security Considerations

- Simple JWT token validation for admin
- Rate limiting per email (for subscriptions)
- Admin password hashing (bcrypt/Argon2)
- Admin route protection
- Stripe webhook signature verification
- Input validation and sanitization
- CORS configuration
- Secrets management

---

## Deployment Checklist

- [ ] Admin password stored in Secrets Manager
- [ ] Stripe account configured
- [ ] New DynamoDB tables created
- [ ] Lambda functions deployed
- [ ] API Gateway routes configured
- [ ] Frontend deployed with new features
- [ ] Environment variables set
- [ ] Webhook endpoints configured
- [ ] Admin access granted
- [ ] Monitoring and alerts set up

