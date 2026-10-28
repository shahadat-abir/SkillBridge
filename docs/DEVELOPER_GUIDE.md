# Developer Guide — SkillBridge

## Architecture
Controllers (thin) → Services (business logic, ownership checks, transactions, notifications, audit) → Domain rules (pure static classes) → EF Core `ApplicationDbContext`.
See [diagrams/02-architecture.svg](diagrams/02-architecture.svg) and [diagrams/04-class.svg](diagrams/04-class.svg).

## Key conventions
* Services return `ServiceResult` / `ServiceResult<T>`; controllers show the message through TempData (`Flash`).
* `NotificationService.Add` and `AuditService.Add` queue entities in the current unit of work; the calling service saves once.
* `TimeProvider` is injected everywhere (tests use `FakeClock`).
* Status transitions only via `ApplicationRules`, `PaymentRules`, `ChallengeRules` and the services.
* Every state change on a job renews `Job.ConcurrencyStamp`.
* Uploads go through `FileStorageService` only.

## Adding a feature
1. Add/modify entities in `Models`, configure in `ApplicationDbContext.OnModelCreating`.
2. Add a migration: `dotnet ef migrations add <Name>`.
3. Put rules in `Domain` (unit test them) and workflow in a service (test with `TestHarness`).
4. Add a thin controller action with `[Authorize(Roles=…)]` and a view.

## Tests
`tests/SkillBridge.Tests` — `TestHarness` builds the real services over EF Core InMemory with a controllable clock.
Run: `dotnet test` or Visual Studio Test Explorer.

## Configuration
| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection (LocalDB by default) |
| `Seed:AdminEmail`, `Seed:AdminPassword` | Admin account (user secrets / environment) |
| `Seed:DemoData`, `Seed:DemoPassword` | Development demo data |

## Folder map
See README "Project structure".
