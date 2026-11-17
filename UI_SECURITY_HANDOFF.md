# SAST/DAST hardening: context for the Angular UI session

**UI project:** `C:\Users\Admin\Desktop\ICICI\ICICI_NEW\ICICI_LombUI\GAL_LombardUI` (branch `sundar_dev`)
**API project:** `C:\Users\Admin\Desktop\ICICI\ICICI_NEW\ICICI_LombAPINew\LombardAPI_NEW` (branch `group_membership`)

## Background

- GAL Search and Account Unlock already passed Checkmarx SAST (GAL_v5: only Information items) and the Burp DAST retest.
- Those API fixes (commits `edc988b`, `7ec06dd`) were only on the API branch `sundar_dev` and had never been merged into `group_membership`.
- The API now has all of them, and the same controls are extended to **Group Membership**, **Rights Management** and **Approval**.
- **Do not change any working functionality in the UI.** Only adapt it to the API behaviour below.

## What the API now does (affects the UI)

| Area | API behaviour now | DAST item |
|---|---|---|
| Authentication | **Every** controller is `[Authorize]`. No / invalid token → **401** with `WWW-Authenticate: Bearer` | Unauthenticated API |
| Group Membership: `GET api/GroupMembership/{dlName}?source=`, `POST api/GroupMembership/AddOrRemoveDLMembership` | Requires the Entra app role **`Feature.Access`**, else **403** (empty body) | Privilege Escalation |
| Rights Management: `GET api/RightsManagement/{dlName}?source=`, `POST api/RightsManagement/AddOrRemoveRights` | Requires **`Feature.Access`**, else **403** (empty body) | Privilege Escalation |
| Account Unlock | Requires `Feature.Access` (unchanged from the cleared build) | Privilege Escalation |
| GAL Search | Any signed-in user (unchanged) | |
| Approval: `GET api/Approval/request/{id}`, `POST approve/{id}`, `POST reject/{id}` | Token required. **No `Feature.Access` needed**, because DL owners are normal users. Only the DL owner the mail went to can act (**403** "Invalid access…" for anyone else) | Privilege Escalation (object-level) |
| Rate limiting (all endpoints, per client IP) | **2 requests / 5 s** burst and **20 requests / 15 min** sustained. Over the limit → **429** (empty body) | Improper Rate Limiting |
| DL name in lookups | Rejected with **400** `"DL name contains invalid characters."` if it contains `"` `“` `”` `„` `` ` `` `$` or control chars, or is longer than 256 chars | PowerShell Injection |
| Missing / invalid `source` on Group Membership GET | **400** `"Invalid source value. Allowed: OnPrem, Cloud."` (previously a 500) | Input validation |
| Approve failure text | No raw PowerShell/AD error any more. E.g. `"Approval failed: Failed to add rights on DL 'X'. The request is still pending."` | Improper Error Handling |
| Unhandled errors | 500 ProblemDetails: `{"title":"An unexpected error occurred.","status":500,"detail":"The request could not be completed. Please contact support if the problem persists."}` | Improper Error Handling |
| Security headers | CSP, X-Frame-Options DENY, HSTS, nosniff, Referrer-Policy, Permissions-Policy, COOP/CORP, Cache-Control no-store on every API response | Security Headers |
| Swagger | Only in Development (404 on UAT/Prod) | |

## UI tasks (please check each; most may already be done on `sundar_dev`)

1. **Menu and route guard for the new screens.**
   - Show the **Group Membership** and **Rights Management** menu items only when `auth.hasFeatureAccess()` (`src/services/auth.ts`) is true, the same as Account Unlock.
   - Add a route guard on `dashboard/group-membership` and `dashboard/rights-management` that redirects users without the role to `gal-search`.
   - Do **not** put this guard on `/approval`. DL owners usually do not have `Feature.Access`; `approval.spec.ts` already asserts this.
2. **403 handling on Group Membership and Rights Management.** The body is empty, so show a fixed message such as "You do not have access to this feature." Never show the raw response.
3. **429 handling (global, in the HTTP error interceptor).** Show "Too many requests. Please wait a few minutes and try again." Do **not** auto-retry; retrying only extends the block.
4. **Stay inside the rate limit (2 requests / 5 s per user IP):**
   - No API call per keystroke on the DL name. Call only on the Search button or blur, with `distinctUntilChanged`.
   - Disable the Search / Submit / Approve / Reject buttons while a request is in flight, which also prevents double-submit.
   - Make sure no screen fires duplicate calls on load: no double subscriptions, no `ngOnInit` plus `ngOnChanges` both loading.
   - Use `shareReplay` where a result is reused.
5. **Client-side validation that mirrors the API** (keep the API as the authority):
   - **DL name:** required, ≤ 256 chars, must not contain `"` `“` `”` `„` `` ` `` `$`.
   - **Employee / Consultant IDs:** comma-separated, each `^[A-Za-z0-9._-]{1,64}$`.
   - **Source:** `OnPrem` or `Cloud` only. Always send it.
   - **Action:** `Add` or `Remove`.
6. **Error display.**
   - Show only `error.message` / `error.Message` / `error.detail` text from the API.
   - If the body is HTML or not JSON (an IIS error page; it starts with `<`), show a generic "Service temporarily unavailable" message.
   - Never render error text with `innerHTML`; use text interpolation only.
7. **No `console.*` in production code** for the new components and services (group-membership, rights-management, approval, and the services they use).
   - The earlier Checkmarx report counted 139 console-leak findings in the bundle.
   - Strip them, or wrap them in `if (!environment.production)`.
   - Verify with `ng build --configuration production` and grep `dist/` for `console.` (excluding `*.map`).
8. **No sensitive data in storage or URLs.** Don't keep tokens, employee IDs or DL owner emails in `localStorage` or query strings beyond what MSAL itself stores. The approval ID in `/approval?id=` is fine (a GUID, and owner-only on the API).
9. **Requestor fields.** The API takes requestor ID and name from the token and ignores body values. Keep sending them for compatibility; don't build UI logic on them.
10. **Hosting headers (UI `web.config` / IIS).** Keep the same security headers on the UI site that cleared the earlier scan: CSP, X-Frame-Options, HSTS, nosniff, Referrer-Policy.

## Server notes (API deployment)

- Deploy the **full** publish output. Keep the server's own `appsettings.json`.
- `appsettings.json` in the repo no longer contains the DB password or SMTP password (same as the cleared build). The server copy is unaffected.
- To run locally, set the environment variable `ConnectionStrings__DefaultConnection`.
- Optional tuning of the rate limit: section `"RateLimiting": { "BurstPermitLimit": 2, "BurstWindowSeconds": 5, "PermitLimit": 20, "WindowMinutes": 15 }`. The defaults equal the cleared build.
- **Users of Group Membership / Rights Management must have the `Feature.Access` app role** in Entra ID (Enterprise app → Users and groups), the same as Account Unlock.
