# Software Requirements Specification — SkillBridge

Version 0.1 · 28 Oct 2026 · Premier University, Chittagong — Dept. of CSE — CSE 3234 (V4) Software Engineering Laboratory

## 1. Introduction
### 1.1 Purpose
This SRS describes the functional and non-functional requirements of SkillBridge, a web-based skill marketplace and job/task recruitment management system.
### 1.2 Scope
SkillBridge lets Employers publish jobs/tasks, lets Hunters discover and apply for them, supports applicant comparison, selection,
assignment, work submission and review, a challenge mode, ratings, notifications and a simulated payment ledger, administered by Admins.
### 1.3 Definitions
| Term | Meaning |
|---|---|
| Employer | User who posts jobs and pays for work |
| Hunter | User who applies for jobs and performs work |
| Admin | Platform administrator |
| Job difficulty | 1–3 star category describing the complexity of the work |
| Skill level | A Hunter's self-reported proficiency in one skill (Beginner, Intermediate, Advanced, Expert) |
| Performance rating | 1–5 stars left by the other party of a completed assignment |
| Challenge | Job mode in which several Hunters perform the same task and the first submission that passes review wins |
| Ledger | Simulated record of payment status; no money is transferred |
### 1.4 Stakeholders
Instructor (Yakinur Rahman, Lecturer, Dept. of CSE) · project team · Employers · Hunters · Administrators.

## 2. Overall description
* Web application, ASP.NET Core MVC (.NET 10), SQL Server, Bootstrap 5, responsive.
* Users: Employer, Hunter, Admin. Visitors can browse jobs anonymously.
* Constraints: no real payment gateway; no email/SMS; runs on SQL Server LocalDB for the demonstration.

## 3. Functional requirements
Requirements marked **[INSTR]** come from the instructor (as relayed by the team); **[DESIGN]** are team design decisions.

| ID | Requirement |
|---|---|
| FR-1 [DESIGN] | Users register as Employer or Hunter and log in; Admins are created by seeding or role change. |
| FR-2 [INSTR] | Employers create jobs with title, description, category, **budget**, payment type, **qualifications/eligibility**, required skills with minimum **skill level**, **difficulty** (1–3 stars), expected completion time, application deadline, project deadline, submission instructions, terms and optional attachment. |
| FR-3 [DESIGN] | Employers edit jobs before any selection, close/reopen them, and cancel them before work starts. |
| FR-4 [DESIGN] | Anyone can search jobs by keyword, category, skill, difficulty, mode, budget range and status, sort and paginate. |
| FR-5 [DESIGN] | Hunters maintain a profile (headline, about, education, experience, portfolio, availability) and skills with per-skill levels. |
| FR-6 [DESIGN] | Hunters apply to open standard jobs before the application deadline with a proposal, price, time and optional document; one application per job; may withdraw while active. |
| FR-7 [INSTR] | Employers **compare applicants** side by side on skill match, rating, completed jobs, price, time, experience, education, portfolio and application date. |
| FR-8 [INSTR] | Employers mark applications under review, shortlisted or rejected and **select one candidate**. |
| FR-9 [INSTR] | Selection creates a **task assignment** the Hunter accepts or declines; on acceptance other applicants are rejected and notified. |
| FR-10 [INSTR] | Hunters **submit work** (description + file and/or link); each submission is a new version with a server timestamp. |
| FR-11 [DESIGN] | Employers approve work (completing the job) or request a revision with a reason; Hunters resubmit. |
| FR-12 [DESIGN] | Challenge mode: up to N participants join after accepting disclosed rules; submissions are reviewed in timestamp order; the first submission that passes review wins; only one winner; others closed. |
| FR-13 [INSTR] | **Star ratings**: after completion each party may rate the other once (1–5 stars + comment). |
| FR-14 [INSTR] | **Notifications** for applications, status changes, selection, acceptance/decline, submissions, revisions, approval, completion, ratings, payments, challenge events and account status. |
| FR-15 [INSTR] | **Payment tracking**: ledger entry per paid assignment with statuses Pending, Awaiting approval, Approved, Paid, Failed, Cancelled, Refunded and full history. |
| FR-16 [DESIGN] | Dashboards for Hunter, Employer and Admin showing figures computed from records. |
| FR-17 [DESIGN] | Admin manages users (role, suspend, reactivate), categories, skills, moderates jobs and reports, views audit log and ledger. |

## 4. Business rules
* BR-1 Applications are accepted only while the job is Open and before the application deadline.
* BR-2 The project deadline must be after the application deadline.
* BR-3 Fixed-price jobs require the proposed price to equal the budget.
* BR-4 Only one active assignment per standard job; conflicting selections are rejected (transaction + concurrency token).
* BR-5 Application status changes follow `ApplicationRules`; payment status changes follow `PaymentRules`.
* BR-6 A payment can be marked Paid only after the work is approved.
* BR-7 Ratings only for Completed assignments, by the Employer or the Hunter of that assignment, once each.
* BR-8 Challenge: registration closes at the application deadline or when full; submissions close at the project deadline; only the earliest pending submission can be decided; the first approval is the single winner; only the winner gets a ledger entry.
* BR-9 Job difficulty, skill level and performance rating are separate concepts in data and UI.

## 5. Non-functional requirements
| ID | Requirement |
|---|---|
| NFR-1 Security | Identity password hashing, role- and ownership-based authorization on the server, anti-forgery on all POSTs, lockout after 5 failed logins, rate limiting, secure cookies, security headers, safe uploads, audit log. |
| NFR-2 Usability | Responsive Bootstrap 5 UI, labelled forms, validation messages, empty states, confirmations for important actions. |
| NFR-3 Integrity | Foreign keys with deliberate delete behaviour, unique constraints, check constraint on ratings, decimal(18,2) money, UTC timestamps. |
| NFR-4 Maintainability | Thin controllers, services with dependency injection, pure domain rules with unit tests. |
| NFR-5 Honesty | No fabricated ratings or payments; demo data labelled [DEMO]; simulated payments labelled. |

## 6. Acceptance criteria
Each requirement is traced to implementation and tests in `REQUIREMENT_TRACEABILITY_MATRIX.md`. The build must succeed and
all automated tests pass (current: 56/56). The end-to-end scenarios E2E-1…E2E-10 in `TEST_RESULTS.md` must pass.
