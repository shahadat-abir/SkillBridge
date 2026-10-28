# Database Design — SkillBridge

Engine: SQL Server (LocalDB for development) · Database: `SkillBridgeDb` · ORM: EF Core 10 · Diagram: [diagrams/03-er.svg](diagrams/03-er.svg)

## Tables
| Table | Purpose | Key constraints |
|---|---|---|
| AspNetUsers (+ Identity tables) | Users and profile fields (Hunter and Employer) | Unique email |
| JobCategories | Admin-managed categories | Unique Name; seeded 7 |
| Skills | Skill catalogue | Unique Name; seeded 15 |
| UserSkills | Hunter skill + level + years | PK (UserId, SkillId); cascade from user |
| Jobs | Job/task posts | FK Category, Employer (Restrict); indexes Status, CategoryId, Difficulty, ApplicationDeadline, EmployerId; `ConcurrencyStamp` concurrency token |
| JobSkills | Required skills + minimum level | PK (JobId, SkillId); cascade from job |
| JobApplications | Applications / bids | Unique (JobId, HunterId) |
| TaskAssignments | Hunter working on a job (standard or challenge participant) | Unique (JobId, HunterId); unique ApplicationId (filtered NOT NULL); **unique filtered index JobId WHERE IsWinner = 1** |
| WorkSubmissions | Versioned deliveries | Unique (TaskAssignmentId, Version); cascade from assignment |
| Ratings | 1–5 star reviews | CHECK Stars BETWEEN 1 AND 5; unique (TaskAssignmentId, RaterId) |
| Payments | Simulated ledger | One per assignment; unique LedgerReference; decimal(18,2) |
| PaymentEvents | Payment status history | Cascade from payment |
| Notifications | In-app notifications | Index (UserId, IsRead); cascade from user |
| AuditLogs | Append-only audit trail | Index CreatedAt, (EntityType, EntityId) |
| ContentReports | Reported jobs | Index Status |

## Design decisions
* **Delete behaviour:** business history (jobs, applications, assignments, ratings, payments) uses `Restrict`; only owned detail rows cascade.
* **Derived values are not stored:** average rating and completed-job counts are computed by `ReputationService`, so users cannot edit them.
* **Money:** `decimal(18,2)`; currency column (BDT).
* **Time:** all timestamps are UTC; the UI converts to local time.
* **Concurrency:** `Job.ConcurrencyStamp` (optimistic concurrency) changes on every state transition. Together with the filtered unique index it guarantees one selection per standard job and one challenge winner.
* **Transactions:** selection, acceptance, decline, challenge join, approval and cancellation run inside database transactions.

## Enumerations (stored as int)
JobDifficulty (1–3) · SkillLevel (1–4) · JobMode · PaymentType · JobStatus · ApplicationStatus · AssignmentStatus · SubmissionStatus · PaymentStatus · PaymentMethod · NotificationType · ReportStatus — see `Models/Enums/Enums.cs`.

## Initialisation
`DbSeeder` applies migrations if present, otherwise `EnsureCreated()`; seeds roles, admin, and (Development) labelled demo data.
