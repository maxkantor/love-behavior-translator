# Love Behavior Translator

React (AWS Amplify) + .NET 8 Lambda + API Gateway + DynamoDB + Secrets Manager + SES + S3.

## Stripe Live Mode

To switch to Stripe live mode for production, see [STRIPE_LIVE_SETUP.md](./STRIPE_LIVE_SETUP.md) for detailed instructions.

This repo mirrors the **pet-behavior-translator** AWS architecture, but adapts prompts/models for **romantic + interpersonal behavior analysis**.

### Safety disclaimer

**This app provides general relationship insights and is not professional therapy or counseling.**

## Repo layout

- `frontend/`: React app (Vite). Deployed with AWS Amplify Hosting.
- `backend/`: .NET 8 Lambda (API Gateway proxy).
- `infra/`: AWS CDK (API Gateway, Lambda, DynamoDB, S3, Secrets Manager, SES permissions).
- `amplify.yml`: Amplify build spec for `frontend/`.

## API

- **POST** `/analyze`
- **GET** `/health`

Request JSON:

```json
{
  "behavior_description": "My partner has been canceling plans...",
  "relationship_type": "dating",
  "relationship_length": "6 months",
  "emotional_state": "anxious",
  "analysis_mode": "gentle",
  "email_to": "name@example.com"
}
```

`analysis_mode` supports: `gentle`, `analytical`, `brutally_honest`, `light_funny`.

## Backend reuse points (same structure as pet-behavior-translator)

- **AI integration**: `backend/src/LoveBehaviorTranslator.Function/Function.cs` (OpenAI call)
- **Prompt building**: `backend/src/LoveBehaviorTranslator.Function/PromptFactory.cs`
- **Request/response models**: `backend/src/LoveBehaviorTranslator.Function/Models.cs`
- **Error handling + logging**: centralized in `Function.cs`
- **Rate limiting**: DynamoDB-backed per-IP counter (TTL)
- **Persistence**: request log in DynamoDB + optional artifact in S3
- **Secrets**: OpenAI key loaded from Secrets Manager at runtime
- **SES**: optional email if `email_to` is provided (requires verified sender)

## Deploy (recommended order)

### 1) Build the Lambda zip

From repo root (Windows PowerShell):

```powershell
.\backend\build.ps1
```

This produces: `backend/dist/function.zip` (CDK references this path).

### 2) Deploy AWS backend (CDK)

```powershell
cd .\infra
npm ci
npm run build
npx cdk bootstrap
npx cdk deploy
```

CDK outputs `ApiBaseUrl`. Use that in Amplify as `VITE_API_BASE_URL`.

### 3) Set the OpenAI secret value

In AWS Secrets Manager, set the secret (created by CDK) to either:

- raw string: `sk-...`
- or JSON: `{"OPENAI_API_KEY":"sk-..."}`

### 4) Configure SES (optional email)

- Verify a sender identity in SES for your region
- Set Lambda env var `SES_FROM_EMAIL` to that verified address (update stack / redeploy)

### 5) Deploy frontend (Amplify Hosting)

In Amplify Console:

- Connect this GitHub repo
- Build uses `amplify.yml`
- Add env var: `VITE_API_BASE_URL = <ApiBaseUrl from CDK>`

## Local dev

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Set `VITE_API_BASE_URL` in your local environment (or a local `.env` in `frontend/`) to point at the deployed API.


