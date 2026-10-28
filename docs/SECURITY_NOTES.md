# Security Notes — SkillBridge

| Control | Implementation | Verified |
|---|---|---|
| Password storage | ASP.NET Core Identity (PBKDF2 hashing); min 8 chars, upper, lower, digit | by framework |
| Account lockout | 5 failed attempts → 10 min lockout | configured |
| Suspension | `IsSuspended` + lockout end = max + security stamp refresh (sign-out within 1 minute) | E2E-10 |
| Authorization | `[Authorize(Roles=…)]` on controllers/actions **and** ownership checks in every service method (employer owns job, hunter owns application/assignment, notification owner) | unit tests + E2E-2 |
| Anti-forgery | Global `AutoValidateAntiforgeryTokenAttribute` | E2E-2 (400 without token) |
| Rate limiting | Fixed window 10 requests/min/IP on Login and Register POST | E2E-10 (429) |
| Cookies | HttpOnly, Secure always, sliding expiration | configured |
| Headers | X-Content-Type-Options nosniff, X-Frame-Options DENY, Referrer-Policy; HSTS outside Development | configured |
| SQL injection | EF Core parameterised LINQ only; no raw SQL | code review |
| XSS | Razor HTML encoding everywhere; no `Html.Raw` of user input; external links use `rel="noopener noreferrer nofollow"` | code review |
| Open redirect | `Url.IsLocalUrl` / `LocalRedirect` for return URLs and notification links | code review |
| File uploads | Extension allow-list, 10 MB limit, magic-byte signature check, random file names, stored in `App_Data/uploads` outside `wwwroot`, served only by authorized actions, path traversal check | unit tests |
| Secrets | No passwords in source; admin/demo passwords from configuration/user-secrets or randomly generated in Development into git-ignored `App_Data/dev-credentials.txt` | review |
| Error handling | Exception handler page outside Development; status-code pages; no stack traces to users in Production | configured |
| Status changes | Only through service methods with transition rules (`ApplicationRules`, `PaymentRules`, `ChallengeRules`) | unit tests |
| Audit | `AuditLogs` for job, application, selection, assignment, submission, review, challenge, payment, rating, admin actions | E2E-10 |
| Dependencies | `System.Security.Cryptography.Pkcs` pinned to 10.0.13 to fix CVE-2026-70162 / CVE-2026-84008 | build: 0 warnings |

## Limitations
* No two-factor authentication or email confirmation (no email service).
* Development uses LocalDB with Windows authentication; production deployment would need a managed SQL Server and secret store.
* Payment is a simulated ledger; never enter card details or passwords in SkillBridge.
