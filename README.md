# MotiveAI App

> *Follow the incentives. Ask not what happened — ask why.*

A serverless, authenticated web application that uses Claude AI to perform incentive-layer analysis on events and decisions. Given any topic, MotiveAI surfaces the underlying pressures, motivations, and timing signals that official narratives tend to obscure.

---

## What It Does

MotiveAI takes an event or decision as input and asks the questions most analysis skips:

- **Who are the key actors?**
- **What pressures were they under?**
- **What did they stand to gain or avoid?**
- **What does the timing reveal?**
- **What is the most likely real motivation?**

The result is structured incentive analysis powered by Claude, delivered through a clean, authenticated interface.

---

## Architecture

```
[React Frontend]  →  [CloudFront / S3]
       ↓
[Cognito Auth]
       ↓
[API Gateway]
       ↓
[Lambda (.NET 8)]
       ↓
[Secrets Manager]  →  [Claude API (Anthropic)]
```

| Layer | Technology |
|---|---|
| Frontend | React + TypeScript + Vite |
| Auth | AWS Cognito (invite-only) |
| Hosting | S3 + CloudFront (HTTPS, global CDN) |
| API | AWS API Gateway |
| Backend | AWS Lambda (.NET 8 / C#) |
| Secrets | AWS Secrets Manager |
| LLM | Anthropic Claude API |
| IaC | AWS CDK (C#) — see [motiveai-infra](https://github.com/mjdevaccount/motiveai-infra) |

---

## Repository Structure

```
motiveai-app/
├── frontend/          # React + Vite application
│   ├── src/
│   │   ├── App.tsx        # Main UI + Amplify Authenticator
│   │   ├── main.tsx       # Amplify configuration entry point
│   │   └── aws-exports.ts # Cognito config (public values)
│   └── package.json
└── lambda/            # .NET 8 Lambda function
    └── MotiveAI.Lambda/
        ├── Function.cs    # Handler — fetches secret, calls Claude
        └── MotiveAI.Lambda.csproj
```

---

## Infrastructure

Infrastructure is managed separately via AWS CDK in C#:

**[motiveai-infra →](https://github.com/mjdevaccount/motiveai-infra)**

Deployed stacks:
- `MotiveAI-Auth` — Cognito User Pool, App Client, Hosted UI
- `MotiveAI-Api` — API Gateway, Lambda, Secrets Manager
- `MotiveAI-Frontend` — S3 bucket, CloudFront distribution

---

## Local Development

### Prerequisites
- Node.js 18+
- .NET 8 SDK
- AWS CLI configured

### Frontend

```bash
cd frontend
npm install
cp .env.example .env        # add your API Gateway URL
npm run dev
```

### Lambda

```bash
cd lambda/MotiveAI.Lambda
dotnet build
dotnet publish -c Release -r linux-x64 --self-contained false -o publish
```

---

## Deployment

### Deploy Lambda + API

```bash
# From motiveai-infra
cdk deploy MotiveAI-Api
```

### Deploy Frontend

```bash
cd frontend
npm run build
aws s3 sync dist s3://motiveai-frontend --delete
aws cloudfront create-invalidation --distribution-id <YOUR_DIST_ID> --paths "/*"
```

### Set Claude API Key

```bash
aws secretsmanager put-secret-value \
  --secret-id motiveai/claude-api-key \
  --secret-string "sk-ant-..."
```

---

## Security

- **Invite-only auth** — no self-signup, users created manually via CLI
- **Secrets Manager** — Claude API key never appears in code or environment files
- **CloudFront** — S3 bucket is fully private, no public access
- **IAM least privilege** — Lambda role scoped to its specific secret only

---

## Tech Stack

**C# / .NET 8** · **React** · **TypeScript** · **AWS CDK** · **Anthropic Claude**

---

*Infrastructure as code in [motiveai-infra](https://github.com/mjdevaccount/motiveai-infra)*
