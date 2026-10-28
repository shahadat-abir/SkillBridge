# SkillBridge — Project Status

_Last updated: 28 Oct 2026_

## Summary
* Build: **succeeds** in Visual Studio 2026 (`Rebuild All: 2 succeeded, 0 failed`), **0 errors, 0 warnings**.
* Tests: **56 passed, 0 failed, 0 skipped**.
* Manual end-to-end run on SQL Server LocalDB: standard workflow, challenge mode, authorization, admin — all passed (docs/TEST_RESULTS.md).

## Done
- Domain model, DbContext, Identity roles, seeder (no hard-coded passwords)
- Jobs (CRUD-like lifecycle), search/filter/sort/paging
- Applications, comparison, selection, assignment accept/decline
- Submissions, revisions, approval, challenge mode
- Simulated payment ledger with history, ratings, notifications, audit log
- Admin dashboard, users, categories/skills, jobs, reports, audit
- 56 automated tests; documentation and 8 diagrams

## Open items / next steps
1. **Confirm instructor rules**: number of difficulty levels (assumed 3 stars) and the challenge winner rule (assumed "first submission that passes review").
2. **Generate the EF migration** (requires a terminal): `dotnet ef database drop --force` then `dotnet ef migrations add InitialCreate`.
3. **Install Git** and make the first commit (`.gitignore` already excludes bin/obj/App_Data/secrets).
4. Exercise file upload, report moderation and category/skill administration end-to-end before the demo.
5. Optional: profile photo upload, dispute workflow, email notifications (only if required).

## Known limitations
- In-app notifications only (no email/SMS).
- Payments are simulated; no gateway.
- Timestamps stored in UTC; displayed in the server's local time zone (Asia/Dhaka on the demo PC).
- Automated workflow tests use EF Core InMemory, which does not enforce SQL Server unique/filtered indexes; those are covered by the LocalDB end-to-end run.
- Login/registration limited to 10 attempts per minute per IP (by design) — repeated demo logins can briefly return HTTP 429.
