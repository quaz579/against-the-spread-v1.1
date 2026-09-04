# Against The Spread v1.1 Deployment

v1.1 deploys the Blazor WebAssembly app and its managed .NET 8 API to Azure Static Web Apps **Free**. Terraform owns the isolated Azure resources in `ats-v11-prod-rg`.

## Production resources

- Static Web App: `ats-v11-prod-web`
- URL: `https://white-river-02b2c0110.3.azurestaticapps.net`
- Storage account: `atsv11prodst`
- Application Insights: `ats-v11-prod-ai`
- Terraform state: private `tfstate/against-the-spread-v1.1.tfstate`

There is no separately deployed production Function App. The API under `src/AgainstTheSpread.Functions` is deployed as the Static Web App's managed API.

## Infrastructure

```bash
cd infrastructure/terraform
terraform init
terraform fmt -check -recursive
terraform validate
terraform plan -var-file=v1.1.tfvars
terraform apply -var-file=v1.1.tfvars
```

Terraform manages the managed API settings:

- `GOOGLE_CLIENT_ID`
- `ADMIN_EMAILS`
- `AZURE_STORAGE_CONNECTION_STRING`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

Do not add `GOOGLE_CLIENT_SECRET` or a Static Web Apps custom `auth` block. This deployment uses Google Identity Services ID tokens validated by the Functions and remains compatible with the Free plan.

## GitHub deployment

The workflow is `.github/workflows/azure-static-web-apps-v1-1.yml`. It requires this repository secret:

```text
AZURE_STATIC_WEB_APPS_API_TOKEN_ATS_V11_PROD
```

The value comes from the sensitive Terraform output `static_web_app_deployment_token`. Never print or commit it.

A push to `main` or manual workflow dispatch builds and deploys both the static app and managed API.

## Verification gate

Before pushing:

1. Run all .NET tests.
2. Run TypeScript validation.
3. Run the mocked GIS Playwright test.
4. Require a no-change Terraform plan.
5. Confirm no token, client secret, storage credential, or request-header logging is present.

After deployment:

1. Verify the production URL and static assets return `200` over valid TLS.
2. Verify public picks/lines routes remain anonymous.
3. Verify missing, malformed, and forged credentials are rejected by all protected API routes.
4. Open `/admin` in a real browser and confirm the Google button reaches the account chooser.
5. Sign in with an allowlisted Google account and exercise weekly and bowl uploads.
6. Sign out and confirm the in-memory credential and upload UI are cleared.
7. Verify Excel generation/download on a physical iPhone installed as a Home Screen PWA.

See `GOOGLE_AUTH_SETUP.md` and `LOCAL_DEV_AUTH.md` for authentication details.
