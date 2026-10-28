# SkillBridge

**Connect Skills. Discover Opportunities. Deliver Results.**

SkillBridge is a web-based skill marketplace and job/task recruitment management system.
Employers post jobs and tasks with budgets, qualifications, required skills and a difficulty (star) category;
Hunters build skill profiles, apply or join challenges, deliver work and build a reputation from completed work.
Employers compare applicants side by side, select a Hunter, review submissions (approve / request revision),
and track payment in a clearly-labelled **simulated** ledger.

### Project team  
Instructor: Yakinur Rahman  
Lecturer, Department of Computer Science and Engineering  
CSE 3234(V4) — Software Engineering Laboratory  
Team: Muhammad Shahadat Hosain Abir, Tuli Dhar, Afra Anan, Suborna Das

## Core features

| Area | What works |
|---|---|
| Roles | Employer, Hunter, Admin (ASP.NET Core Identity, server-side role + ownership checks) |
| Jobs | Title, description, category, required skills + minimum skill level, 1–3 star difficulty, budget, payment type, expected days, application deadline, project deadline, eligibility, submission instructions, terms, attachment, status, timestamps |
| Search | Keyword, category, skill, difficulty, mode, budget range, status, sorting, pagination (all against the database) |
| Applications | Proposal, proposed price/time, relevant experience, attachment; lifecycle Submitted → Under review → Shortlisted → Accepted / Rejected / Withdrawn; one application per Hunter per job |
| Applicant comparison | Side-by-side table: skill-match lines, performance rating, completed jobs, price, time, experience, education, portfolio, availability, cover letter; informational highlights only (no invented score) |
| Selection & assignment | Transactional selection with concurrency protection; Hunter accepts/declines; other applicants notified |
| Work & revisions | Versioned submissions (file and/or link), employer feedback, revision requests, resubmission, approval, completion |
| Challenge mode | N participants do the same task; rules disclosed before joining; submissions reviewed in server-timestamp order; first submission that passes review wins; single winner guaranteed |
| Ratings | 1–5 stars + review, only for completed work, once per party |
| Payments | Simulated ledger: Pending → Awaiting approval → Approved → Paid (+ Failed, Cancelled, Refunded) with full history |
| Notifications | Database-backed notification centre with unread badge |
| Admin | Dashboard, users (role change, suspend/reactivate), categories & skills, job moderation, reports, audit log, ledger |

## Technology stack

C# · ASP.NET Core MVC (.NET 10, SDK 10.0.401) · Entity Framework Core 10 · SQL Server LocalDB · ASP.NET Core Identity ·
Razor views · Bootstrap 5 · jQuery validation · xUnit · EF Core InMemory (tests).

## Prerequisites

* .NET 10 SDK
* SQL Server LocalDB (installed with SQL Server Express / Visual Studio)
* Visual Studio 2026 or VS Code with C# Dev Kit
* Optional: `dotnet tool install --global dotnet-ef` (for migrations)

## Setup and run

```powershell
cd E:\SkillBridge
dotnet restore
dotnet build
dotnet run --launch-profile https
```

Open https://localhost:7180.
The database `SkillBridgeDb` is created automatically on LocalDB on first run
(connection string in `appsettings.json`).

### Database initialisation and migrations

`Data/DbSeeder.cs` applies EF Core migrations when the project contains them, otherwise it falls back to
`EnsureCreated()`. To switch to migrations (recommended before final submission):

```powershell
dotnet ef database drop --force        # removes the database created by EnsureCreated (demo data only)
dotnet ef migrations add InitialCreate
dotnet run
```

### Accounts for the demonstration

No passwords are stored in source code.

* In **Development**, the first run creates an administrator (`admin@skillbridge.local`) and, because
  `Seed:DemoData` is `true` in `appsettings.Development.json`, four clearly-labelled **[DEMO]** accounts
  and two demo jobs. Random passwords are generated and written to `App_Data/dev-credentials.txt`
  (git-ignored).
* To choose your own passwords instead:
  `dotnet user-secrets set "Seed:AdminPassword" "YourStrongPassw0rd"` and `dotnet user-secrets set "Seed:DemoPassword" "..."`
  before the first run.
* Anyone can register as an Employer or a Hunter from the Register page. Admin accounts are never self-registered.

## Running the tests

```powershell
dotnet test
```

or **Test → Run All Tests** in Visual Studio. 56 tests (unit + service-level workflow tests).

## Payment disclaimer

SkillBridge does **not** integrate a payment gateway, does not store card details and does not transfer money.
“Paid” means the Employer recorded an off-platform payment. Ledger references (`SIM-…`) are internal identifiers,
not gateway transaction IDs.

## Known limitations

See [PROJECT_STATUS.md](PROJECT_STATUS.md). In short: the instructor's original requirement document was not
available to the development team (challenge-mode rules are a documented interpretation); no email/SMS delivery;
no real payment gateway; no EF migration has been generated yet (EnsureCreated fallback); Git is not installed on the
development PC, so there is no commit history yet.

## Project structure

```
SkillBridge.slnx
SkillBridge.csproj          web application
Authorization/              role names
Controllers/                MVC controllers (thin; delegate to services)
Data/                       ApplicationDbContext, DbSeeder
Domain/                     pure business rules (unit-tested)
Models/  Models/Enums/      entities and enums
Services/                   business services (ownership checks, transactions, notifications, audit)
ViewModels/                 form/view models and UI helpers
ViewComponents/             notification bell
Views/                      Razor views
wwwroot/                    CSS, JS, Bootstrap, jQuery validation
tests/SkillBridge.Tests/    xUnit tests
docs/                       SRS, database design, test plan/results, security notes, guides, diagrams
```

## Documentation

* [Requirement traceability matrix](REQUIREMENT_TRACEABILITY_MATRIX.md)
* [Project audit](PROJECT_AUDIT.md) · [Project status](PROJECT_STATUS.md) · [Changelog](CHANGELOG.md)
* [SRS](docs/SRS.md) · [Database design](docs/DATABASE_DESIGN.md) · [Diagrams](docs/DIAGRAMS.md)
* [Test plan](docs/TEST_PLAN.md) · [Test results](docs/TEST_RESULTS.md) · [Security notes](docs/SECURITY_NOTES.md)
* [User guide](docs/USER_GUIDE.md) · [Developer guide](docs/DEVELOPER_GUIDE.md)
