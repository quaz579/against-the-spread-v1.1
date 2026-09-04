# Local Authentication Development

The v1.1 admin flow uses application-owned Google Identity Services tokens. SWA CLI mock principals are intentionally unsupported because the Functions no longer trust `X-MS-CLIENT-PRINCIPAL`.

## Automated local testing

No localhost Google origin is required for the normal test suite:

- Function authorization tests inject a fake token validator.
- Blazor component tests mock JavaScript interop and the same-origin admin API.
- `tests/specs/admin-gis.spec.ts` injects a fake GIS browser object and intercepts `/api/current-admin`.

The browser test credential is synthetic, never leaves localhost, and cannot authenticate to production.

Run the .NET tests:

```bash
dotnet test AgainstTheSpread.sln
```

Run TypeScript validation and Playwright after starting the local app/SWA environment:

```bash
cd tests
npm exec -- tsc -p tsconfig.json --noEmit
npm test
```

Five legacy upload E2E fixtures are skipped because they depended on SWA CLI's forged principal. Their backend authorization and upload entry paths are covered by .NET tests; final upload verification uses a real allowlisted Google account on the deployed origin.

## Optional real GIS testing on localhost

Only add localhost under **Authorized JavaScript origins** if a developer specifically needs to exercise Google's real widget locally. Google documents adding both `http://localhost` and the exact port origin, such as `http://localhost:4280`.

A redirect URI and client secret are not needed for the GIS JavaScript-callback flow.

Production verification must still run on:

```text
https://white-river-02b2c0110.3.azurestaticapps.net
```
