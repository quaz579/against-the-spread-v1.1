# Authentication Architecture Note

This document supersedes the historical `X-MS-CLIENT-PRINCIPAL` workaround.
That approach depended on Azure Static Web Apps custom provider configuration,
which is not available on the Free plan and is not trusted by the current API.

## Current flow

1. The admin page uses Google Identity Services with the Web OAuth client ID.
2. GIS returns a short-lived Google ID token to the browser callback.
3. The token remains in component memory; it is never placed in a URL, cookie,
   local storage, or session storage.
4. The browser sends `Authorization: Bearer <token>` only to `/api/admin/me`,
   `/api/upload-lines`, and `/api/upload-bowl-lines`.
5. The managed .NET 8 API validates Google's signature, issuer, expiry, exact
   audience, and `email_verified`, then checks the normalized email against
   `ADMIN_EMAILS`.
6. Authentication failures return generic `401`; authenticated non-admin users
   receive generic `403`. Identity responses and protected requests use
   `Cache-Control: no-store`.

Both upload endpoints authorize before parsing the request body. Public picks,
lines, and score APIs remain anonymous.

See `GOOGLE_AUTH_SETUP.md` and `LOCAL_DEV_AUTH.md` for setup and test guidance.
