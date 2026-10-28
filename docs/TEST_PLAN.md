# Test Plan — SkillBridge

## A. Build verification
Restore and rebuild the solution in Visual Studio 2026 (or `dotnet build`); record errors and warnings.

## B. Unit tests (`tests/SkillBridge.Tests/RulesTests.cs`)
| Area | Tests |
|---|---|
| Job eligibility & deadlines | JobRulesTests (open/closed/deadline, date ordering, budget, challenge participant bounds) |
| Application status transitions | ApplicationRulesTests (7 cases) |
| Payment status transitions | PaymentRulesTests (9 cases) |
| Required-skill matching | SkillMatcherTests |
| Challenge ordering / capacity | ChallengeRulesTests |
| Upload validation | FileSignatureTests |

## C. Service-level integration tests (`WorkflowTests.cs`, EF Core InMemory)
Application once/deadline/own job/fixed price; ownership of selection and single selection; comparison data;
full lifecycle (accept → submit → revision → resubmit → approve → pay → rate); rating eligibility & range; decline and reselect;
outsider cannot accept/submit; submission needs file or link; challenge winner rules and payment; challenge deadline;
employer cannot join own challenge; notification privacy; search filters; job ownership.

## D. Manual end-to-end tests (running app + SQL Server LocalDB)
E2E-1 apply/duplicate/validation · E2E-2 authorization & anti-forgery · E2E-3 comparison page · E2E-4 standard lifecycle ·
E2E-5 ratings · E2E-6 challenge mode · E2E-7 notifications · E2E-8 page smoke test for all roles · E2E-9 job creation & validation ·
E2E-10 admin suspend/reactivate, audit, dashboard, rate limiting.

## E. Regression
After each fix: rebuild and run all tests (`Test → Run All Tests`).

## Not covered / limitations
Load testing of concurrent winner selection; browser compatibility matrix; file upload end-to-end; email/SMS (not implemented).
