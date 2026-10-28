# Test Results — SkillBridge

Date: 28 Oct 2026 · Machine: DESKTOP-0LCRLUH (Windows x64) · Visual Studio Community 2026 18.10.2 · .NET SDK 10.0.401 · SQL Server LocalDB

## A. Build
| Run | Result |
|---|---|
| Rebuild solution (first) | `Rebuild All: 2 succeeded, 0 failed, 0 skipped` — 0 errors, 4 warnings (NU1902/NU1903: System.Security.Cryptography.Pkcs 9.0.4 via Microsoft.Data.SqlClient) |
| After pinning Pkcs 10.0.0 | still NU1902/NU1903 (10.0.0 also affected) |
| After pinning Pkcs 10.0.13 | `Rebuild All: 2 succeeded, 0 failed` — **0 errors, 0 warnings** |

## B/C. Automated tests
`Test run finished: 56 Tests (56 Passed, 0 Failed, 0 Skipped) run in 4.2 sec` (Visual Studio Test Explorer).

> Note: one code change was made after this run (audit entity type for work submissions, `AssignmentService.SubmitAsync`).
> The rebuild and re-run after that change are recorded in section E.

## D. Manual end-to-end tests (app running from Visual Studio, https://localhost:7180, LocalDB)
Performed through the real controllers using form posts with anti-forgery tokens and page views in the browser pane, with clearly-labelled demo accounts.

| ID | Scenario | Expected | Actual | Result |
|---|---|---|---|---|
| E2E-1 | Hunter 1 applies to job 1; applies again; Hunter 2 applies; invalid application to job 2 | success; duplicate refused; success; validation errors | "Your application was submitted"; "You have already applied to this job"; success; 3 validation messages (cover letter length, price range, days range) | Pass |
| E2E-2 | Hunter opens Compare, Admin, Jobs/Create; posts Select; POST without anti-forgery token | access denied ×4; HTTP 400 | AccessDenied ×4; 400 | Pass |
| E2E-3 | Employer opens comparison sorted by skill match | real data, highlights | Nusrat 3/3 (100%, best skill match), Rafiq 1/3 (33%, lowest price); no ratings shown as "No ratings yet" | Pass |
| E2E-4 | Select → second select → Hunter accept → submit v1 → Hunter self-approve → revision → v2 → approve → mark paid | as per rules | selected; second refused; accepted; submitted; self-approve AccessDenied; revision requested; v2 submitted; approved ("job is complete"); payment recorded (Paid); other applicant status = Rejected; outsider view of assignment = AccessDenied | Pass |
| E2E-5 | Employer rates 5★, rates again, Hunter rates 4★ | first OK, duplicate refused, OK | as expected | Pass |
| E2E-6 | Challenge: join without accepting rules; 3 Hunters join & submit (order H2, H1, H3); approve H1 first; reject H2; approve H1 | refused; joins OK; out-of-order refused; reject OK; H1 winner; others closed; one ledger entry | exactly as expected; ledger: challenge 3,000 BDT **Approved** for winner only; participants list: 1 Winner, 2 "Challenge closed (not winner)"; no review buttons left | Pass |
| E2E-7 | Hunter notification centre | notifications for events | 7 notifications for Hunter 1 | Pass |
| E2E-8 | Page smoke test: 11 anonymous, 12 Hunter, 13 Employer, 8 Admin URLs | 200, no exception; unknown job 404 | all 200; `/Jobs/Details/999` → 404 | Pass |
| E2E-9 | Employer creates job via form; invalid job | created; validation errors | Job #3 published; 6 validation messages incl. "Project deadline must be after the application deadline" and "Select at least one required skill" | Pass |
| E2E-10 | Admin suspends Hunter 3; Hunter 3 logs in; reactivate; login; audit; dashboard; login burst | blocked, then allowed; audit entries; real figures; 429 | "This account is suspended…"; login OK after reactivation; 22 audit rows; Users 5, Employers/Hunters 1/3, Jobs 1/3 open, Completed 2, Ledger paid/due 14,000/3,000; HTTP 429 after >10 logins/min | Pass |

## E. Regression after final code change
| Step | Result |
|---|---|
| Rebuild solution after the `AssignmentService` audit fix (09:16) | succeeded (subsequent incremental build: 2 up-to-date, 0 failed) |
| Run all tests | `Test run finished: 56 Tests (56 Passed, 0 Failed, 0 Skipped) run in 3.8 sec` |

## Skipped / not tested
* File uploads end-to-end (signature validation covered by unit tests only).
* Report moderation and category/skill administration actions (pages render; actions not exercised).
* Simultaneous winner race under real concurrency (protected by concurrency token + filtered unique index; not load-tested).
