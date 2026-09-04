# Google Identity Services Admin Authentication

The v1.1 admin page uses Google Identity Services (GIS) on the Azure Static Web Apps Free plan. The browser receives a short-lived signed Google ID token and sends it only to the protected same-origin APIs. The Functions validate the token and enforce the admin allowlist.

## Google Cloud configuration

Use the existing Web application OAuth client.

Under **Authorized JavaScript origins**, retain the v1 origin and add:

```text
https://white-river-02b2c0110.3.azurestaticapps.net
```

Do not add a redirect URI for this JavaScript-callback flow. Do not remove the existing v1 callback configuration.

The OAuth client ID is public and is present in the frontend configuration. The Google client secret is not used and must not be added to the repository, browser configuration, or Azure settings.

If the consent screen is in Testing mode, keep the intended admin Google account in the test-user list.

## Azure configuration

Terraform configures these managed-API application settings on `ats-v11-prod-web`:

- `GOOGLE_CLIENT_ID`: exact expected Google token audience.
- `ADMIN_EMAILS`: comma-separated allowlist of verified Google email addresses.
- `AZURE_STORAGE_CONNECTION_STRING`: private game-file storage.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: telemetry.

Apply settings through `infrastructure/terraform/main.tf`; do not configure SWA custom authentication. The Free plan does not support the old custom `auth` block.

## Authorization behavior

- Missing, malformed, expired, wrongly signed, or wrong-audience credentials return `401`.
- Tokens without a verified, non-empty email return `401`.
- A valid Google identity not present in `ADMIN_EMAILS` returns `403`.
- `/api/current-admin`, `/api/upload-lines`, and `/api/upload-bowl-lines` require
  the Google ID token in `X-Google-ID-Token`. The managed SWA proxy replaces
  `Authorization`, so the application must not use that header for this token.
- The identity route avoids Azure Functions' reserved `/api/admin*` prefix.
- Public picks and lines APIs remain anonymous.
- `X-MS-CLIENT-PRINCIPAL` is not trusted.

## Verification

1. Open `/admin` on the deployed v1.1 origin.
2. Confirm the rendered Google button reaches Google's account chooser.
3. Sign in with an allowlisted account.
4. Confirm the admin page displays the server-verified email.
5. Exercise both upload forms.
6. Sign out and confirm the credential is discarded and the upload UI disappears.

Never put Google credentials in logs, URLs, screenshots, `localStorage`, or `sessionStorage`.
