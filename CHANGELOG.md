# Changelog

## 0.1.0 — 28 Oct 2026
### Added
- SkillBridge ASP.NET Core MVC application (.NET 10) with Identity roles Employer / Hunter / Admin.
- Jobs with budget, qualifications, required skills and minimum levels, 1–3 star difficulty, deadlines, attachments.
- Search/filter/sort/paging; applications with lifecycle; applicant comparison; transactional Hunter selection.
- Assignment accept/decline; versioned work submission; revision requests; approval and completion.
- Challenge mode (multiple participants, first submission that passes review wins, single-winner guarantee).
- Simulated payment ledger with history; 1–5 star ratings; notification centre; audit log; content reports.
- Admin area: dashboard, users (roles, suspend/reactivate), categories & skills, jobs, reports, audit.
- Security: anti-forgery on all POSTs, rate-limited login/registration, lockout, secure cookies, security headers, safe uploads.
- xUnit test project (56 tests); documentation and Mermaid diagrams.
### Fixed
- Pinned `System.Security.Cryptography.Pkcs` 10.0.13 (CVE-2026-70162, CVE-2026-84008 reported for 9.0.4 via Microsoft.Data.SqlClient).
- Audit entry for work submission now references the assignment entity type.
### Renamed
- Legacy name "FindYourJob — SkillBridge" replaced by the official name **SkillBridge**.
