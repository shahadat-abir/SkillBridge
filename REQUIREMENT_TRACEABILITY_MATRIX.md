# SkillBridge — Requirement Traceability Matrix

**Source legend**
* **INSTR** — instructor requirement as relayed by the team (exact document not available to the developer; wording below is the team's summary).
* **BRIEF** — project brief / master prompt written by the team (design decisions and expectations, not instructor wording).

**Status legend:** Missing · In Progress · Implemented · Verified (implemented **and** tested: automated test and/or manual end-to-end run on SQL Server LocalDB, 28 Oct 2026) · Blocked.

Test IDs refer to `tests/SkillBridge.Tests` (class.method) and manual end-to-end checks `E2E-n` in `docs/TEST_RESULTS.md`.

## A. Instructor requirements (INSTR)

| ID | Original requirement (team summary) | Interpretation | Module | Entities | UI pages | Backend logic | Test case | Status | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| REQ-001 | Job posting with budgets | Employer creates a job with a positive budget (BDT), payment type fixed/negotiable | Jobs | Job | Jobs/Create, Jobs/Edit, Jobs/Details | JobService.CreateAsync/UpdateAsync, JobRules.Validate | JobRulesTests.Budget_must_be_positive; E2E-9 | Verified | Job #3 created through the form; invalid budget rejected |
| REQ-002 | Job posting with qualifications | Eligibility criteria / qualifications text plus required skills with minimum levels | Jobs | Job.EligibilityCriteria, JobSkill | Jobs/Form, Jobs/Details | JobService.SetSkillsAsync | E2E-9 ("Select at least one required skill") | Verified | Form validation output |
| REQ-003 | Skill levels | Each Hunter skill has its own level (Beginner/Intermediate/Advanced/Expert), self-reported; jobs require a minimum level per skill | Profile, Jobs | UserSkill, JobSkill, SkillLevel | Profile/Skills, Jobs/Details, Applications/Compare | ProfileService.AddOrUpdateSkillAsync, SkillMatcher | SkillMatcherTests.*; ApplicationWorkflowTests.Comparison_uses_real_skill_and_rating_data | Verified | Compare page shows per-skill lines |
| REQ-004 | Difficulty levels | Job difficulty as a star category (1–3 stars), separate from skill level | Jobs | Job.Difficulty (JobDifficulty) | Jobs/Form, job cards, filters | JobService.SearchAsync | NotificationAndJobTests.Search_filters_against_real_data | Verified | ⚠ Number of levels (3) assumed — confirm with instructor |
| REQ-005 | Star ratings | 1–5 star performance rating + review for completed work, once per party; shown on profiles and comparison | Ratings | Rating (check constraint 1–5, unique per rater/assignment) | Ratings/Create, Profile/Details, Compare | RatingService, ReputationService | AssignmentWorkflowTests.Full_lifecycle…, Rating_is_not_allowed_before_completion, Rating_must_be_between_one_and_five; E2E-5 | Verified | Duplicate rating refused |
| REQ-006 | Applicant comparison | Side-by-side comparison of applicants for one job using real evidence; Employer decides | Applications | JobApplication, UserSkill, Rating, TaskAssignment | Applications/Compare | ApplicationService.CompareAsync | ApplicationWorkflowTests.Comparison_uses_real_skill_and_rating_data, Other_employer_cannot_compare_applicants; E2E-3 | Verified | Screenshot reviewed during E2E-3 |
| REQ-007 | Candidate selection | Employer selects one applicant (standard mode); conflicting selections prevented; Hunter notified | Applications | JobApplication, TaskAssignment, Job.ConcurrencyStamp | Compare, ForJob | ApplicationService.SelectAsync (transaction) | Only_job_owner_can_select_and_only_one_hunter_is_selected; E2E-4 | Verified | Second selection refused |
| REQ-008 | Task assignment | Selected Hunter receives an assignment, accepts or declines; others rejected on acceptance | Assignments | TaskAssignment | Assignments/Details, Assignments/Index | AssignmentService.AcceptAsync/DeclineAsync | Declining_reopens_the_job…, Another_hunter_cannot_accept_or_submit; E2E-4 | Verified | |
| REQ-009 | Work submission | Hunter submits versioned work (file and/or link); employer approves or requests revision; history kept | Assignments | WorkSubmission | Assignments/Submit, Assignments/Details | AssignmentService.SubmitAsync/ApproveAsync/RequestRevisionAsync | Full_lifecycle…, Submission_requires_a_file_or_link; E2E-4 | Verified | v1 revision → v2 approved |
| REQ-010 | Notifications | In-app notifications for key events; users see only their own | Notifications | Notification | Notifications/Index, navbar bell | NotificationService | Users_cannot_read_other_users_notifications; E2E-7 | Verified | Hunter had 7 notifications after workflow |
| REQ-011 | Payment tracking | Ledger per assignment with statuses and history; simulated, honestly labelled | Payments | Payment, PaymentEvent | Payments/Index, Details, MarkPaid | PaymentService, PaymentRules | PaymentRulesTests.*; Full_lifecycle… (cannot pay before approval); E2E-4, E2E-6 | Verified | Ledger: SIM-… Paid 14,000; Approved 3,000 |

## B. Project brief requirements (BRIEF)

| ID | Requirement | Module | Entities | UI pages | Backend logic | Test case | Status | Evidence / notes |
|---|---|---|---|---|---|---|---|---|
| REQ-101 | Three roles: Employer, Hunter, Admin; self-registration only as Employer/Hunter | Account | Identity roles | Account/Register, Login | AccountController, AppRoles | E2E-1, E2E-2 | Verified | |
| REQ-102 | Server-side authorization (roles + ownership) | All | — | — | [Authorize] + service ownership checks | Other_employer_cannot_compare…, Employer_cannot_edit_or_cancel…, Another_hunter_cannot…; E2E-2 | Verified | Hunter → Compare/Admin/Create/Select = Access denied |
| REQ-103 | Employer can edit, close, reopen, cancel eligible jobs | Jobs | Job | Jobs/Details, Edit | JobService.Close/Reopen/Cancel/Update | Employer_cannot_edit_or_cancel_another_employers_job | Verified | |
| REQ-104 | Applications not accepted after deadline or closure | Jobs | Job | — | JobRules.IsAcceptingApplications | JobRulesTests.*, Cannot_apply_after_application_deadline | Verified | |
| REQ-105 | Application deadline distinct from project deadline (validated) | Jobs | Job | Jobs/Form | JobRules.Validate | Project_deadline_must_be_after_application_deadline; E2E-9 | Verified | |
| REQ-106 | Search & filter: keyword, category, skill, difficulty, budget, status, sort, pagination | Jobs | Job | Jobs/Index | JobService.SearchAsync | Search_filters_against_real_data; E2E-8 | Verified | |
| REQ-107 | Application lifecycle; duplicate active applications prevented; status changed only by authorized operations | Applications | JobApplication (unique JobId+HunterId) | Applications/Mine, ForJob | ApplicationRules, ApplicationService | ApplicationRulesTests.*, Hunter_can_apply_once…; E2E-1 | Verified | |
| REQ-108 | Hunter profile: introduction, skills + levels, education, experience, portfolio, availability, completed work, reviews | Profile | ApplicationUser, UserSkill | Profile/Details, Edit, Skills | ProfileService | E2E-8 (pages render) | Implemented | Skills labelled "self-reported". No profile image upload. |
| REQ-109 | Challenge mode: multiple participants, rules disclosed, first valid submission wins, single winner under concurrency control, history kept | Challenge | Job (Mode, MaxParticipants, WinnerAssignmentId), TaskAssignment (IsWinner, filtered unique index) | Jobs/Details (rules box), Assignments/Challenge | AssignmentService.JoinChallenge/Approve/RejectChallengeSubmission, ChallengeRules | ChallengeWorkflowTests.*, ChallengeRulesTests.*; E2E-6 | Verified (interpretation) | ⚠ Interpretation: Employer reviews in timestamp order and the first submission that **passes review** wins. Confirm with instructor whether earliest upload alone should win. |
| REQ-110 | Only winner is paid in challenge mode | Payments | Payment | — | AssignmentService.ApproveAsync | First_valid_submission_wins_and_only_winner_is_paid; E2E-6 | Verified | |
| REQ-111 | Admin: dashboard, user/role management, suspend/reactivate | Admin | ApplicationUser | Admin/Index, Users | AdminController | E2E-10 | Verified | Suspended user cannot log in |
| REQ-112 | Admin: categories & skills management | Admin | JobCategory, Skill | Admin/Categories | AdminController | E2E-10 (page renders) | Implemented | Add/enable/disable not exercised end-to-end |
| REQ-113 | Admin: moderation of reported content | Admin | ContentReport | Jobs/Details (report), Admin/Reports | JobService.ReportAsync, AdminController.ResolveReport | — | Implemented | Not exercised end-to-end |
| REQ-114 | Audit records for sensitive operations | Audit | AuditLog | Admin/Audit | AuditService | E2E-10 | Verified | 22 entries after the workflow |
| REQ-115 | Dashboards from real data, scoped to user | Dashboard | — | Dashboard/Hunter, Employer, Admin/Index | DashboardService | E2E-8, E2E-10 | Verified | Admin: Users 5, Ledger paid/due 14,000/3,000 |
| REQ-116 | Secure file uploads (extension, size, signature, outside wwwroot, authorized download) | Files | — | Apply, Submit, Job form | FileStorageService | FileSignatureTests.* | Implemented | Upload not exercised end-to-end (links used) |
| REQ-117 | Anti-forgery, rate limiting, lockout, security headers, no secrets in source | Security | — | — | Program.cs, DbSeeder | E2E-2 (400 without token), E2E-10 (429 after burst) | Verified | |
| REQ-118 | Payments never marked Paid without approval; no card data; no fake transaction IDs | Payments | Payment | MarkPaid | PaymentRules, PaymentService.MarkPaidAsync | Transitions_follow_the_ledger_rules, Full_lifecycle… | Verified | |
| REQ-119 | Dispute handling | — | — | — | — | — | Missing (out of scope) | Admin can cancel jobs; no dispute workflow. Implement only if the instructor requires it. |
| REQ-120 | EF Core migrations | Data | — | — | DbSeeder (Migrate if present) | — | Blocked | Migration must be generated with `dotnet ef migrations add InitialCreate` (assistant has no terminal access). EnsureCreated fallback works. |

## Totals

| Status | INSTR (11) | BRIEF (20) |
|---|---|---|
| Verified | 11 | 14 (one marked "interpretation") |
| Implemented (not fully tested end-to-end) | 0 | 4 |
| Missing | 0 | 1 |
| Blocked | 0 | 1 |
