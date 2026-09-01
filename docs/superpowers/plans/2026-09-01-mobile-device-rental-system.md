# Mobile Device Rental Management System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved internal mobile-device rental system with company-email accounts, role-aware device management, atomic lending, time-window enforcement, audited administration, reliable notifications, and the approved responsive UI.

**Architecture:** Use one .NET 10 modular monolith and one PostgreSQL 18 database. Domain and application code remain framework-independent; Infrastructure owns ASP.NET Core Identity, EF Core/Npgsql, image processing, private storage, audit persistence, and the transactional outbox. Web, Worker, and AdminCli are separate composition roots that share application services but do not reference one another.

**Tech Stack:** .NET SDK 10.0.400 / ASP.NET Core 10.0.11, Razor Pages, EF Core 10.0.11, Npgsql 10.0.3, PostgreSQL 18, SkiaSharp 4.151.1 plus `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.151.1, xUnit v3 4.0.0, Microsoft Playwright 1.62.0, coverlet 10.0.1, Tailwind CSS 4.3.3, HTMX 2.0.10, Lucide 1.37.0, axe 4.13.0, k6 1.8.1, GitHub Actions.

---

## Execution Rules

- Before Task 1, commit the approved v1.0 docs/preview/plan as the first `main` baseline, verify `.worktrees/` is ignored, then create `.worktrees/mobile-device-rental-system` on branch `codex/mobile-device-rental-system`. All implementation tasks run inside that worktree, never directly on `main`.
- Use test-first RED -> GREEN -> REFACTOR for every behavior-bearing change.
- Do not replace PostgreSQL tests with SQLite or EF InMemory.
- Integration tests read `DEVICERENTAL_TEST_POSTGRES_ADMIN`; if absent, they may use Testcontainers only when Docker is available. If neither exists, fail with an actionable prerequisite error rather than skipping.
- Every command handler uses a fresh DI scope and DbContext. Concurrent tests never share a DbContext or NpgsqlConnection.
- The repository, package locks, images, and container images never contain real domains, administrator emails, SMTP credentials, storage credentials, or Data Protection keys. Production runtime receives these values through environment/secret providers or one-time secure AdminCli input. Development examples live only in `appsettings.Development.json` and synthetic test seeds.
- Create one local commit after each task. Do not push production, import employee data, or deploy without a separate approval.
- Push each task commit to the already-authorized feature branch so PG18/GitHub-hosted gates can run. For a PostgreSQL behavior that cannot run locally, first commit and push the RED test alone, record the expected failing CI job, then commit/push the implementation and record GREEN. Never push an intentionally failing commit to `main`.
- Every .NET command uses `pwsh eng/dotnet.ps1 <arguments>`. The wrapper resolves the shared repository-local SDK from `git rev-parse --git-common-dir`, or a PATH SDK that satisfies `global.json`. New machines run `eng/Bootstrap-DotNet.ps1`; commands never depend on a variable from a previous shell cell.
- Every approved behavior test declares `[Trait("Requirement", "REQ-... or NFR-...")]` and every MVP case declares `[Trait("MvpCase", "AUTH-001 etc.")]`. When a case needs several test methods, exactly one method is the primary `MvpCase` owner and the rest carry only Requirement traits. Each task updates `docs/traceability.csv` and `docs/mvp-test-cases.csv` from `Planned` to `Implemented`/`Passing` as its tests land.
- Stage exact paths only. `git add .` is forbidden. Feature-branch push is an explicit final handoff step; merging to `main`, three post-merge `main` CI runs, production deployment, and real-data import remain separate user-approved release gates.

## Approved Requirement Coverage Matrix

Create `docs/traceability.csv` in Task 1 with one row per ID and columns `RequirementId,ImplementationTask,PrimaryNamedTest,Status`. CI parses the authoritative IDs from `docs/requirements-specification.md`, fails on a missing/duplicate/unknown row, and later fails when an approved `[M]` test is skipped. The following mapping is the implementation contract:

| Task | Requirement IDs | Primary named test families |
| --- | --- | --- |
| Task 5 | `REQ-AUTH-001`, `REQ-AUTH-002`, `REQ-AUTH-003`, `REQ-AUTH-004`, `REQ-AUTH-005`, `REQ-AUTH-006`, `REQ-AUTH-007`, `REQ-AUTH-008`, `REQ-AUTH-009` | `RegistrationTests`, `EmailVerificationTests`, `PasswordResetTests`, `AuthenticationSessionTests`, `AdminGuardConcurrencyTests` |
| Task 6 | `REQ-DEV-001`, `REQ-DEV-002`, `REQ-DEV-003`, `REQ-DEV-004`, `REQ-DEV-005`, `REQ-DEV-006`, `REQ-DEV-007`, `REQ-DEV-008`, `REQ-DEV-009`, `REQ-DEV-010` | `ImageValidationTests`, `DeviceCatalogCommandTests`, `DeviceCommandConcurrencyTests`, `PrivacyResponseTests` |
| Task 7 | `REQ-LOAN-001`, `REQ-LOAN-002`, `REQ-LOAN-003`, `REQ-LOAN-004`, `REQ-LOAN-005` | `BorrowDeviceTransactionTests`, `BorrowDeviceConcurrencyTests`, `BorrowEndpointConcurrencyTests` |
| Task 8 | `REQ-LOAN-006`, `REQ-LOAN-007`, `REQ-LOAN-008`, `REQ-LOAN-009`, `REQ-LOAN-010`, `REQ-LOAN-011`, `REQ-LOAN-012`, `REQ-LOAN-013`, `REQ-LOAN-014`, `REQ-ADMIN-001`, `REQ-ADMIN-002` | `ReturnAuthorizationTests`, `ReturnConcurrencyTests`, `ForceReturnAndDisableTests`, `LoanExtensionTests`, `PolicyVersionConcurrencyTests`, `LoanHistoryQueryTests` |
| Task 4 | `REQ-TIME-001`, `REQ-TIME-002`, `REQ-TIME-003`, `REQ-TIME-004`, `REQ-TIME-005`, `REQ-TIME-006`, `REQ-TIME-007`, `REQ-AUDIT-001`, `REQ-AUDIT-002`, `REQ-AUDIT-003` | `AccessWindowPolicyTests`, `InteractiveCommandTransactionTests`, `AuditFieldWhitelistTests`, `AtomicAuditOutboxTests` |
| Task 9 | `REQ-NOTIFY-001`, `REQ-NOTIFY-002`, `REQ-NOTIFY-003`, `REQ-NOTIFY-004`, `REQ-NOTIFY-005`, `REQ-NOTIFY-006`, `REQ-NOTIFY-007`, `REQ-NOTIFY-008` | `NotificationTemplateTests`, `ForceReturnNotificationTests`, `SelfReturnNotificationTests`, `OutboxClaimConcurrencyTests`, `OutboxSendingCasTests`, `DeliveryFailureClassifierTests` |
| Task 10 | `REQ-AUDIT-004`, `REQ-AUDIT-005`, `NFR-COMP-001`, `NFR-A11Y-001` | `AuditAuthorizationTests`, `ArchivedHistoryQueryTests`, `ResponsiveDeviceListTests`, `AccessibilitySmokeTests`, `KeyboardJourneyTests` |
| Task 5 / 10 / 11 | `NFR-SEC-001`, `NFR-SEC-002`, `NFR-SEC-003`, `NFR-SEC-004`, `NFR-SEC-005`, `NFR-SEC-006` | `AuthenticationSecurityTests`, `PrivacyResponseTests`, `SecurityHeadersTests`, `SecurityEventAlertTests`, `DataProtectionKeyRingTests` |
| Task 3 / 4 / 11 | `NFR-REL-001`, `NFR-REL-002`, `NFR-REL-003` | `LoanConstraintsTests`, `AtomicMutationRollbackTests`, `BackupRestoreRehearsalTests` |
| Task 7 / 11 | `NFR-PERF-001`, `NFR-PERF-002` | `PerformanceSloSmokeTests`, `BorrowDeviceConcurrencyTests`, `performance/k6/hot-device.js` |
| Task 11 | `NFR-AVL-001`, `NFR-OBS-001` | `AvailabilitySliTests`, `OperationalMetricsTests`, `BackupFreshnessAlertTests`, `ClockDriftAlertTests` |

All 70 IDs appear exactly once as the primary owner in `docs/traceability.csv`; secondary tasks may add additional test coverage without duplicating ownership.

## Proposed File Map

```text
Mobile-Device-Rental-Management-System.slnx
global.json
Directory.Build.props
Directory.Packages.props
NuGet.config
package.json
package-lock.json
src/DeviceRental.Web/Styles/tailwind.input.css
README.md
.github/workflows/ci.yml
docs/traceability.csv
docs/mvp-test-cases.csv
docs/architecture/threat-model.md
docs/architecture/data-dictionary.md
docs/architecture/adr-index.md
eng/Bootstrap-DotNet.ps1
eng/dotnet.ps1
eng/Verify-Prerequisites.ps1
eng/Verify-CiSkeleton.ps1
eng/container-images.json
src/
  DeviceRental.Domain/
    Common/
    Devices/
    Lending/
    Auditing/
    Notifications/
  DeviceRental.Application/
    Abstractions/
    Identity/
    Devices/
    Lending/
    Policy/
    Audit/
    Notifications/
  DeviceRental.Infrastructure/
    DependencyInjection.cs
    Options/
      IdentityOptions.cs
      StorageOptions.cs
      SmtpOptions.cs
      WorkerOptions.cs
      DataProtectionOptions.cs
    Persistence/
      DeviceRentalDbContext.cs
      Configurations/
      Locks/
      Migrations/
      Queries/
    Identity/
    Images/
    Notifications/
    Auditing/
  DeviceRental.Web/
    Dockerfile
    Authorization/
    Middleware/
    Pages/
    Presentation/
    TagHelpers/
    ViewComponents/
    wwwroot/
  DeviceRental.Worker/
    Dockerfile
  DeviceRental.AdminCli/
tests/
  DeviceRental.Testing/
  DeviceRental.UnitTests/
  DeviceRental.IntegrationTests/
  DeviceRental.WebTests/
  DeviceRental.E2ETests/
performance/k6/
deploy/
  .dockerignore
  compose.yaml
  compose.production.yaml
scripts/
  backup.sh
  restore.sh
  backup.ps1
  restore.ps1
```

## Task 1: Toolchain, Solution Skeleton, and Architecture Guardrails

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `NuGet.config`
- Create: `package.json`
- Create: `package-lock.json`
- Create: `eng/Bootstrap-DotNet.ps1`
- Create: `eng/dotnet.ps1`
- Create: `Mobile-Device-Rental-Management-System.slnx`
- Create: `eng/Verify-Prerequisites.ps1`
- Create: `eng/Verify-CiSkeleton.ps1`
- Create: `eng/container-images.json`
- Create: `src/DeviceRental.Domain/DeviceRental.Domain.csproj`
- Create: `src/DeviceRental.Application/DeviceRental.Application.csproj`
- Create: `src/DeviceRental.Infrastructure/DeviceRental.Infrastructure.csproj`
- Create: `src/DeviceRental.Web/DeviceRental.Web.csproj`
- Create: `src/DeviceRental.Worker/DeviceRental.Worker.csproj`
- Create: `src/DeviceRental.AdminCli/DeviceRental.AdminCli.csproj`
- Create: `tests/DeviceRental.Testing/DeviceRental.Testing.csproj`
- Create: `tests/DeviceRental.UnitTests/DeviceRental.UnitTests.csproj`
- Create: `tests/DeviceRental.IntegrationTests/DeviceRental.IntegrationTests.csproj`
- Create: `tests/DeviceRental.WebTests/DeviceRental.WebTests.csproj`
- Create: `tests/DeviceRental.E2ETests/DeviceRental.E2ETests.csproj`
- Create: `tests/DeviceRental.UnitTests/Architecture/ProjectReferenceTests.cs`
- Create: `tests/DeviceRental.UnitTests/Architecture/ApprovedRequirementCoverageTests.cs`
- Create: `tests/DeviceRental.UnitTests/Architecture/ApprovedMvpCaseCoverageTests.cs`
- Create: `docs/traceability.csv`
- Create: `docs/mvp-test-cases.csv`
- Create: `.github/workflows/ci.yml`
- Create: `README.md`

- [ ] **Step 0: Verify the isolated feature worktree**

Before any project generation:

```powershell
git branch --show-current
git rev-parse --git-dir
git rev-parse --git-common-dir
git status --short --branch
```

Expected: branch `codex/mobile-device-rental-system`, linked worktree under `.worktrees/mobile-device-rental-system`, and a clean worktree. If not, stop and correct isolation before continuing.

- [ ] **Step 1: Pin the SDK and central package versions**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Create `Directory.Build.props` with `net10.0`, nullable, implicit usings, warnings-as-errors, deterministic builds, invariant globalization disabled, and generated XML docs disabled for tests. Create `Directory.Packages.props` with central versions for EF/Identity `10.0.11`, Npgsql/EF provider `10.0.3`, SkiaSharp `4.151.1`, xUnit v3 `4.0.0`, Test SDK `18.9.0`, Testcontainers PostgreSQL `4.14.0`, Playwright xUnit v3 `1.62.0`, and `coverlet.collector` `10.0.1`; test projects use TRX logging and XPlat coverage.

Also include `SkiaSharp.NativeAssets.Linux.NoDependencies` `4.151.1`. Create `package.json` with exact dev dependencies `tailwindcss@4.3.3`, `@tailwindcss/cli@4.3.3`, and `@axe-core/playwright@4.13.0`; exact dependencies `htmx.org@2.0.10` and `lucide@1.37.0`; and deterministic vendoring/build scripts. Commit `package-lock.json`; CI uses `npm ci`.

Create `eng/container-images.json` with exact version tags for PostgreSQL 18, .NET 10 SDK/ASP.NET runtime, Mailpit, and k6 1.8.1. Resolve each registry manifest once, store the full sha256 digest, and require all Dockerfiles/workflows/compose files to reference `name:tag@sha256:...`; the guard test fails on tag-only or `latest` references.

- [ ] **Step 2: Implement and smoke-test the repository tool wrappers before first use**

Create `eng/Bootstrap-DotNet.ps1` to download Microsoft's signed `dotnet-install.ps1`, require a Valid Microsoft Authenticode signature, install SDK 10.0.400 into the main checkout's ignored `.tools/dotnet`, and verify `dotnet --info` reports SDK 10.0.400/runtime 10.0.11.

Create `eng/dotnet.ps1` to resolve `git rev-parse --git-common-dir`, locate `<main-checkout>/.tools/dotnet/dotnet.exe`, fall back to PATH only when `dotnet --version` satisfies `global.json`, set telemetry/no-logo variables, forward every argument and exit code, and never modify system/user PATH.

Run before scaffolding:

```powershell
pwsh eng/dotnet.ps1 --version
pwsh eng/Verify-Prerequisites.ps1 -Mode LocalUnit
```

Expected: `10.0.400`; LocalUnit mode permits missing PostgreSQL but reports DB tests unavailable. A separate `-Mode Database` invocation must fail until PG18/Docker is available.

- [ ] **Step 3: Scaffold the projects and references**

Run with the repository-local SDK:

```powershell
pwsh eng/dotnet.ps1 new sln --format slnx -n Mobile-Device-Rental-Management-System
pwsh eng/dotnet.ps1 new classlib -n DeviceRental.Domain -o src/DeviceRental.Domain --framework net10.0
pwsh eng/dotnet.ps1 new classlib -n DeviceRental.Application -o src/DeviceRental.Application --framework net10.0
pwsh eng/dotnet.ps1 new classlib -n DeviceRental.Infrastructure -o src/DeviceRental.Infrastructure --framework net10.0
pwsh eng/dotnet.ps1 new webapp -n DeviceRental.Web -o src/DeviceRental.Web --framework net10.0
pwsh eng/dotnet.ps1 new worker -n DeviceRental.Worker -o src/DeviceRental.Worker --framework net10.0
pwsh eng/dotnet.ps1 new console -n DeviceRental.AdminCli -o src/DeviceRental.AdminCli --framework net10.0
```

Create Testing, UnitTests, IntegrationTests, WebTests, and E2ETests. Set references exactly as follows:

```text
Domain <- Application <- Infrastructure
Web -> Application + Infrastructure
Worker -> Application + Infrastructure
AdminCli -> Application + Infrastructure
UnitTests -> Domain + Application
Testing -> Application + Infrastructure
IntegrationTests -> Testing + Infrastructure
WebTests -> Testing + Web
E2ETests -> Testing + Web
```

- [ ] **Step 4: Write the failing architecture tests**

Create tests that load project reference graphs and assert:

```csharp
[Fact]
public void Domain_HasNoProjectReferences() =>
    Assert.Empty(ProjectReferences.For("DeviceRental.Domain"));

[Fact]
public void CompositionRoots_DoNotReferenceEachOther()
{
    Assert.DoesNotContain("DeviceRental.Worker", ProjectReferences.For("DeviceRental.Web"));
    Assert.DoesNotContain("DeviceRental.AdminCli", ProjectReferences.For("DeviceRental.Worker"));
}
```

Create `ApprovedRequirementCoverageTests` before the CSV. Parse requirement definitions from `docs/requirements-specification.md`, parse `docs/traceability.csv`, and assert identical sets, unique IDs, known task numbers, nonempty named tests, and no status other than `Planned`/`Implemented`/`Passing`.

Create `ApprovedMvpCaseCoverageTests` before its CSV. Parse every `[M]` case ID from the functional matrices in `docs/test-plan.md`, require exactly 72 unique rows in `docs/mvp-test-cases.csv`, and require columns `MvpCaseId,ImplementationTask,PrimaryNamedTest,Status`.

- [ ] **Step 5: Run RED, then implement the graph reader and fix references**

Run:

```powershell
pwsh eng/dotnet.ps1 test tests/DeviceRental.UnitTests --filter FullyQualifiedName~ProjectReferenceTests
```

Expected RED: graph reader and both coverage CSVs are missing. Implement the XML graph reader, create all 70 requirement rows and all 72 MVP case rows, and make all architecture/coverage test classes pass without weakening them.

- [ ] **Step 6: Implement and verify the pinned CI skeleton**

Create `.github/workflows/ci.yml` with SHA-pinned checkout/setup-dotnet/upload-artifact, `build-unit`, and a PostgreSQL-service `integration` skeleton using the digest from `eng/container-images.json`. Create `eng/Verify-CiSkeleton.ps1` to fail on floating action tags, `latest`/tag-only containers, missing required jobs, missing PG health check, or absent `permissions: contents: read`.

```powershell
pwsh eng/Verify-CiSkeleton.ps1
```

Expected: PASS before the workflow is first pushed.

- [ ] **Step 7: Verify the skeleton**

```powershell
pwsh eng/dotnet.ps1 restore --use-lock-file
pwsh eng/dotnet.ps1 build --configuration Release --no-restore
pwsh eng/dotnet.ps1 test tests/DeviceRental.UnitTests --configuration Release --no-build
```

Expected: build succeeds; architecture tests pass; package lock files are generated.

- [ ] **Step 8: Commit**

```powershell
git add global.json Directory.Build.props Directory.Packages.props NuGet.config package.json package-lock.json Mobile-Device-Rental-Management-System.slnx src tests eng docs/traceability.csv docs/mvp-test-cases.csv README.md
git commit -m "build: scaffold modular .NET solution"
```

- [ ] **Step 9: Push the scaffold feature branch and establish CI**

```powershell
git push -u origin codex/mobile-device-rental-system
```

Expected: SHA-pinned `build-unit` and PostgreSQL-18-backed `integration` workflow skeleton jobs execute on the feature branch. The integration job may contain no DB tests yet, but its service/readiness/provider guards must pass. This establishes the remote ref required for later PG18 RED/GREEN cycles.

## Task 2: Domain Policies and Pure Business Rules

**Files:**
- Create: `src/DeviceRental.Domain/Devices/Device.cs`
- Create: `src/DeviceRental.Domain/Devices/DeviceTier.cs`
- Create: `src/DeviceRental.Domain/Devices/ManualDeviceState.cs`
- Create: `src/DeviceRental.Domain/Devices/DeviceImageMetadata.cs`
- Create: `src/DeviceRental.Domain/Lending/Loan.cs`
- Create: `src/DeviceRental.Domain/Lending/ReturnKind.cs`
- Create: `src/DeviceRental.Domain/Common/Reason.cs`
- Create: `src/DeviceRental.Domain/Common/DurationMinutes.cs`
- Create: `src/DeviceRental.Domain/Lending/LoanExtension.cs`
- Create: `src/DeviceRental.Domain/Lending/LoanPolicyVersion.cs`
- Create: `src/DeviceRental.Domain/Lending/LoanExtensionPolicy.cs`
- Create: `src/DeviceRental.Domain/Auditing/AuditEvent.cs`
- Create: `src/DeviceRental.Domain/Notifications/OutboxMessage.cs`
- Create: `src/DeviceRental.Domain/Notifications/NotificationDelivery.cs`
- Create: `src/DeviceRental.Domain/Notifications/NotificationSendOutcome.cs`
- Create: `src/DeviceRental.Domain/Notifications/OutboxStatus.cs`
- Create: `src/DeviceRental.Domain/Notifications/ReminderSchedulePolicy.cs`
- Create: `src/DeviceRental.Domain/Notifications/DeliveryFailureClassifier.cs`
- Create: `src/DeviceRental.Application/Policy/AccessWindowPolicy.cs`
- Create: `src/DeviceRental.Application/Identity/CorporateEmailPolicy.cs`
- Test: `tests/DeviceRental.UnitTests/Policy/AccessWindowPolicyTests.cs`
- Test: `tests/DeviceRental.UnitTests/Lending/LoanStatusPolicyTests.cs`
- Test: `tests/DeviceRental.UnitTests/Lending/LoanExtensionPolicyTests.cs`
- Test: `tests/DeviceRental.UnitTests/Notifications/ReminderSchedulePolicyTests.cs`
- Test: `tests/DeviceRental.UnitTests/Notifications/DeliveryFailureClassifierTests.cs`
- Test: `tests/DeviceRental.UnitTests/Identity/CorporateEmailPolicyTests.cs`

- [ ] **Step 1: Write failing access-window tests**

Cover Shanghai boundaries at `08:59:59.999`, `09:00:00`, `18:59:59.999`, and `19:00:00`, including next-open calculation. The wished-for API is:

```csharp
public sealed class AccessWindowPolicy
{
    public AccessWindowDecision Evaluate(DateTimeOffset utcNow);
}
```

Run the focused tests and verify RED because `AccessWindowPolicy` does not exist.

- [ ] **Step 2: Implement the minimal access policy**

Use a resolved `TimeZoneInfo` for `Asia/Shanghai`, interval `[09:00, 19:00)`, and return both `IsOpen` and `NextOpenUtc`. Never read system time inside the policy.

- [ ] **Step 3: Write failing device and loan status tests**

Cover:

```text
open loan -> Borrowed
overdue open loan -> device remains Borrowed; loan is Overdue
no open loan + TEMP_DISABLED -> Unavailable
no open loan + NORMAL -> Available
archived device -> not borrowable
```

- [ ] **Step 4: Implement immutable device and loan status derivation**

Add `Device.GetAvailability(openLoan)` and `Loan.GetStatus(effectiveNow)`. Do not add mutable `Borrowed` or `Overdue` columns/properties. Run the focused tests GREEN.

- [ ] **Step 5: Write failing extension and reminder tests**

Test extension base `max(oldDueAt, effectiveNow)`, range 60..10080 minutes, maximum `effectiveNow + 7 days`, and reminder boundaries `2h4m59s` (skip) vs `2h5m` (create at +5m).

- [ ] **Step 6: Implement extension and reminder policies**

Implement pure functions that accept all time inputs explicitly. Run `LoanExtensionPolicyTests` and `ReminderSchedulePolicyTests` GREEN before continuing.

- [ ] **Step 7: Write failing email and notification-classification tests**

Test exact IDNA/lowercase corporate-domain matching and explicit outcomes:

```csharp
public enum NotificationSendOutcome
{
    Accepted,
    TransientNotAccepted,
    PermanentRejected,
    AcceptanceUnknown
}
```

AcceptanceUnknown must require manual review, never automatic retry.

- [ ] **Step 8: Implement email normalization and delivery classification**

Use `IdnMapping.GetAscii`, lowercase exact-domain comparison, and explicit delivery outcomes. Run `CorporateEmailPolicyTests` and `DeliveryFailureClassifierTests` GREEN.

- [ ] **Step 9: Run the full unit suite and commit**

```powershell
pwsh eng/dotnet.ps1 test tests/DeviceRental.UnitTests --configuration Release
git add src/DeviceRental.Domain src/DeviceRental.Application tests/DeviceRental.UnitTests
git commit -m "feat: define device lending domain policies"
```

## Task 3: PostgreSQL 18 Test Environment, Identity Model, and Database Migrations

**Files:**
- Create: `tests/DeviceRental.Testing/PostgresTestEnvironment.cs`
- Create: `tests/DeviceRental.Testing/DatabaseReset.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/DeviceRentalDbContext.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs`
- Generate with EF: migration `IdentityAndAccessPolicy` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Generate with EF: migration `AuditAndOutbox` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Test: `tests/DeviceRental.IntegrationTests/Migrations/MigrationSmokeTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Database/IdentityOutboxConstraintTests.cs`
- Test: `tests/DeviceRental.UnitTests/Architecture/PostgreSqlProviderGuardTests.cs`
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Write the fail-fast PostgreSQL fixture tests**

The fixture must:

```csharp
var admin = Environment.GetEnvironmentVariable("DEVICERENTAL_TEST_POSTGRES_ADMIN");
// If configured, connect and create a unique test database.
// Otherwise start postgres:18 with Testcontainers only when Docker is available.
// If neither is available, throw InvalidOperationException with setup instructions.
```

After connecting, run `SHOW server_version_num` and require major version 18.

- [ ] **Step 2: Run RED locally**

```powershell
pwsh eng/dotnet.ps1 test tests/DeviceRental.IntegrationTests --filter Category=Database
```

Expected RED on this workstation: explicit prerequisite failure because Docker/PostgreSQL is absent. This proves the environment gate only; it is not the RED evidence for database behavior.

- [ ] **Step 3: Write the real failing migration and base-constraint tests in CI**

Before model configuration/migrations exist, run against the CI PostgreSQL 18 service and verify RED for the expected reasons:

```text
MigrationSmokeTests.LatestMigration_AppliesToEmptyPostgreSql18 -> no migration/model
IdentityOutboxConstraintTests.NormalizedEmail_IsRequiredAndUnique -> constraint absent
IdentityOutboxConstraintTests.OutboxDedupeKey_IsRequiredAndUnique -> constraint absent
IdentityOutboxConstraintTests.InvalidLeaseTuple_IsRejected -> CHECK absent
```

`PostgreSqlProviderGuardTests` scans package/project files and fails if `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.InMemory`, `UseSqlite`, or `UseInMemoryDatabase` appears outside the guard itself.

Commit and push only the fixture/tests/provider guard plus CI service changes:

```powershell
git add tests/DeviceRental.Testing tests/DeviceRental.IntegrationTests/Migrations/MigrationSmokeTests.cs tests/DeviceRental.IntegrationTests/Database/IdentityOutboxConstraintTests.cs tests/DeviceRental.UnitTests/Architecture/PostgreSqlProviderGuardTests.cs .github/workflows/ci.yml
git commit -m "test: specify PostgreSQL identity and outbox schema"
git push
```

Expected remote RED: the integration job starts PG18 successfully, then fails because the model/migrations/constraints are absent. Save the CI run URL in the task log before implementing Step 4.

- [ ] **Step 4: Implement the base DbContext and configurations**

Use `DeviceRentalDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`, schema `device_rental`, UTC `timestamptz`, UUID keys, and `DeleteBehavior.Restrict` for core FKs.

At this task, map only Identity, AuditEvent, and OutboxMessage. Enforce normalized email and outbox dedupe/status/lease constraints. Device and lending constraints are owned by Tasks 6 and 7; notification-delivery operational indexes are owned by Task 9.

- [ ] **Step 5: Create and verify the two base migrations**

Create only:

```text
IdentityAndAccessPolicy
AuditAndOutbox
```

Never use `EnsureCreated`. Tests call `Database.MigrateAsync()`. Save a migration SQL script and verify an empty DB migration plus a down/up or forward-recovery path in CI.

- [ ] **Step 6: Add runtime PostgreSQL 18 fail-fast checks**

`eng/Verify-Prerequisites.ps1`, migration startup, and `/health/ready` execute `SHOW server_version_num` and reject a non-18 server. No deployment/readiness path may choose a different EF provider. The Task 1 integration job's admin connection may only create/drop the unique test DB; tests create/use a separate least-privilege application role.

- [ ] **Step 7: Commit and push the GREEN implementation**

Inspect `git status`; stage the new DbContext/entities/configurations, only the generated `IdentityAndAccessPolicy`/`AuditAndOutbox` migration pairs plus model snapshot, fixture/test updates, prerequisite/readiness changes, and CSV status changes. Then:

```powershell
git commit -m "feat: add PostgreSQL persistence baseline"
git push
```

- [ ] **Step 8: Observe remote GREEN**

Expected: the same CI integration job that produced RED now applies migrations to empty PG18, passes base constraints/provider/version/readiness tests, and reports zero skips. Record the green run URL before Task 4.

## Task 4: Interactive Command Pipeline, Time Gate, Audit, and Atomic Outbox Writes

**Files:**
- Create: `src/DeviceRental.Application/Abstractions/IInteractiveCommandExecutor.cs`
- Create: `src/DeviceRental.Application/Abstractions/BusinessLockPlan.cs`
- Create: `src/DeviceRental.Application/Abstractions/SystemActor.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/InteractiveCommandExecutor.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Locks/PostgresAccountLockService.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Locks/PostgresMaintenanceLockService.cs`
- Create: `src/DeviceRental.Infrastructure/Auditing/AuditWriter.cs`
- Create: `src/DeviceRental.Infrastructure/Notifications/OutboxWriter.cs`
- Create: `src/DeviceRental.Web/Middleware/AccessWindowMiddleware.cs`
- Modify: `src/DeviceRental.Web/Program.cs`
- Test: `tests/DeviceRental.IntegrationTests/Transactions/InteractiveCommandTransactionTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Transactions/AtomicAuditOutboxTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Transactions/AtomicMutationRollbackTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Transactions/MaintenanceLockProtocolTests.cs`
- Test: `tests/DeviceRental.UnitTests/Audit/AuditFieldWhitelistTests.cs`
- Test: `tests/DeviceRental.WebTests/Time/AccessWindowMiddlewareTests.cs`

- [ ] **Step 1: Write failing transaction and time tests**

Use a `CountingTimeProvider` and a tagged account `SELECT ... FOR UPDATE` interceptor. Hold the actor row lock, advance fake time by ten minutes, release it, and assert exactly one lock-after `GetUtcNow()` supplies the access check and committed audit/outbox timestamps.

At `18:59:59`, hold the actor lock, advance to `19:00:00`, release, and assert rollback with no partial audit/outbox rows. Device/loan-specific effective-time assertions are deliberately owned by Task 7 after its migration exists.

Write middleware RED tests for all `REQ-TIME-*` behavior: reads at 18:59:59 may complete; requests at 19:00 return readable 503/Problem Details with reopenAt; direct HTTP and old cookies cannot bypass; health/readiness remain available; a forged `SystemActor` header is ignored; Worker/backup system commands use an internal DI-created actor; restart/cross-midnight/weekend/month-end/leap-day decisions are deterministic.

- [ ] **Step 2: Implement the command executor**

The executor must open `READ COMMITTED`, lock in this order:

```text
shared maintenance advisory lock -> actor account -> optional device -> optional open loan -> pending outbox
```

Every interactive transaction first acquires `pg_advisory_xact_lock_shared(MAINTENANCE_LOCK_KEY)`. The backup coordinator later acquires the exclusive session lock with the identical constant; PostgreSQL then drains existing shared holders and blocks new writers. After the shared lock, read one `effectiveNow`, recheck account/role/access window, execute the handler, save, and commit. No controller may bypass it.

The Task 4 PostgreSQL lock service implements the actor and pending-outbox stages. Task 7 extends the same service with device/open-loan SQL after the `LendingAndLoanPolicy` migration; it must not create a second executor or alternate order.

Implement `AccessWindowMiddleware` and register it in Web `Program.cs` before authentication-protected endpoints. Only static files, `/health/live`, `/health/ready`, and internal composition-root system work are exempt. `SystemActor` has no public binder/header constructor. Run middleware RED tests GREEN.

Write RED `MaintenanceLockProtocolTests.InteractiveCommand_WaitsWhileExclusiveMaintenanceLockHeld`: connection A obtains the exclusive session lock; command B starts and produces no mutation/audit/outbox; release A; B proceeds atomically. Implement `PostgresMaintenanceLockService` and run GREEN.

- [ ] **Step 3: Test atomic audit and outbox failures**

Inject database failures during audit and outbox insert. Assert the main mutation rolls back. Do not mock the DbContext in these tests.

Add `AuditFieldWhitelistTests` before `AuditWriter`: each supported event projects only approved fields and rejects password/token/cookie/whole-entity payloads. Implement the whitelist and run GREEN.

- [ ] **Step 4: Verify and commit**

Run local unit tests plus CI PostgreSQL transaction tests, then:

```powershell
git add src/DeviceRental.Application/Abstractions src/DeviceRental.Infrastructure/Persistence/InteractiveCommandExecutor.cs src/DeviceRental.Infrastructure/Persistence/Locks/PostgresAccountLockService.cs src/DeviceRental.Infrastructure/Persistence/Locks/PostgresMaintenanceLockService.cs src/DeviceRental.Infrastructure/Auditing/AuditWriter.cs src/DeviceRental.Infrastructure/Notifications/OutboxWriter.cs src/DeviceRental.Web/Middleware/AccessWindowMiddleware.cs src/DeviceRental.Web/Program.cs tests/DeviceRental.IntegrationTests/Transactions tests/DeviceRental.UnitTests/Audit/AuditFieldWhitelistTests.cs tests/DeviceRental.WebTests/Time/AccessWindowMiddlewareTests.cs docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: enforce atomic interactive commands"
```

## Task 5: Accounts, ASP.NET Core Identity, Authorization Version, and Admin CLI

**Files:**
- Create: `src/DeviceRental.Application/Identity/AccountSnapshot.cs`
- Create: `src/DeviceRental.Application/Identity/IAccountStore.cs`
- Create: `src/DeviceRental.Application/Identity/IAccountApplicationService.cs`
- Create: `src/DeviceRental.Application/Identity/AccountApplicationService.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/IdentityAccountStore.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/AuthorizationVersionCookieValidator.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/IdentityEmailTokenService.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/IdentityRateLimiter.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/AdminGuardService.cs`
- Create: `src/DeviceRental.AdminCli/Commands/BootstrapAdminCommand.cs`
- Create: `src/DeviceRental.AdminCli/Commands/SetAdminRoleCommand.cs`
- Create: `src/DeviceRental.AdminCli/Commands/SetAccountStatusCommand.cs`
- Modify: `src/DeviceRental.AdminCli/Program.cs`
- Modify: `src/DeviceRental.Infrastructure/DependencyInjection.cs`
- Create: `src/DeviceRental.Web/Pages/Account/Login.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/Login.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/Register.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/Register.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/ConfirmEmail.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/ConfirmEmail.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/ForgotPassword.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/ForgotPassword.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/ResetPassword.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/ResetPassword.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/Logout.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/Logout.cshtml.cs`
- Create: `src/DeviceRental.Web/Authorization/CurrentAccountRequirement.cs`
- Create: `src/DeviceRental.Web/Authorization/CurrentAccountHandler.cs`
- Create: `src/DeviceRental.Web/Authorization/TestAdminRequirement.cs`
- Create: `src/DeviceRental.Web/Authorization/TestAdminHandler.cs`
- Create: `src/DeviceRental.Infrastructure/Options/IdentityOptions.cs`
- Create: `src/DeviceRental.Infrastructure/Options/DataProtectionOptions.cs`
- Test: `tests/DeviceRental.WebTests/Configuration/IdentityConfigurationValidationTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/DataProtectionKeyRingTests.cs`
- Test: `tests/DeviceRental.UnitTests/Identity/AccountApplicationServiceTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Identity/AdminGuardConcurrencyTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Identity/DisabledBorrowerExceptionTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/RegistrationTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/EmailVerificationTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/PasswordResetTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/AuthenticationSessionTests.cs`
- Test: `tests/DeviceRental.WebTests/Identity/AuthenticationSecurityTests.cs`

- [ ] **Step 1: Write failing registration, verification, reset, and lockout tests**

Cover 12/128 password bounds, leak-list rejection, exact corporate domain, 24h email verification, 30m reset token, 30m idle/12h absolute cookie, five failures in 15m, 15m lockout, three verification/reset emails per hour, and generic enumeration-safe responses.

`AuthenticationSecurityTests` must RED for Secure/HttpOnly/SameSite cookies, session rotation, logout invalidation, password-hash parameter upgrade after successful login, fixed dummy hash work for nonexistent users, generic status/body/timing bounds, and Data Protection cookie/token interoperability across two Web instances.

Also write configuration RED tests: Production startup rejects empty/`example.corp` domain allowlists, missing Data Protection key path/protector, writable-in-image key paths, or keys stored under the content root. Development accepts synthetic values only when `ASPNETCORE_ENVIRONMENT=Development`.

- [ ] **Step 2: Implement Identity without leaking role assignment**

Registration DTO contains only email, real name, and password. It ignores/rejects role fields. Store `authorization_version`; include it in the cookie; on every protected request compare current DB version/status/role.

Use ASP.NET Core's password hasher with approved parameters and rehash-on-login; configure strict cookie properties and server-side session validation. Run `AuthenticationSecurityTests` GREEN.

Bind strongly typed options with `ValidateOnStart`. Persist Data Protection keys to a runtime-mounted directory, protect keys at rest using the configured certificate/key provider, and verify two Web instances can read the same cookie while a missing/incompatible key ring fails readiness.

- [ ] **Step 3: Write failing concurrent administrator-guard tests**

Two administrators mutually demote/disable at a barrier. Expected: exactly one commits, no deadlock/5xx, one active administrator remains.

Add RED `DisabledBorrowerExceptionTests`: disabling a borrower invalidates sessions but does not close/release the open loan; the loan remains queryable with `BorrowerStatus=Disabled`, history remains intact, and only an administrator can resolve it through normal force-return flows.

- [ ] **Step 4: Implement AdminCli and advisory guard**

Acquire one transaction advisory lock, sort actor/target UUID row locks, recount admins, update role/status + authorization version + security stamp + audit atomically. The bootstrap command is idempotent and never accepts passwords in command-line arguments. Account disable deliberately leaves loans unchanged and exposes the disabled-borrower flag to later loan queries; run both admin-guard and disabled-borrower tests GREEN.

- [ ] **Step 5: Implement account Razor Pages and middleware**

Add login/register/confirm/resend/forgot/reset/logout with anti-forgery, `InteractiveCommandPipeline`, the approved 503 behavior, and Data Protection keys persisted outside the container filesystem.

- [ ] **Step 6: Verify and commit**

Run Unit/Web tests locally and PostgreSQL Identity tests in CI. Commit:

```powershell
git add src/DeviceRental.Application/Identity src/DeviceRental.Infrastructure/Identity src/DeviceRental.Infrastructure/Options/IdentityOptions.cs src/DeviceRental.Infrastructure/Options/DataProtectionOptions.cs src/DeviceRental.Infrastructure/DependencyInjection.cs src/DeviceRental.AdminCli src/DeviceRental.Web/Pages/Account src/DeviceRental.Web/Authorization tests/DeviceRental.UnitTests/Identity tests/DeviceRental.IntegrationTests/Identity tests/DeviceRental.WebTests/Identity tests/DeviceRental.WebTests/Configuration/IdentityConfigurationValidationTests.cs docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: add company account and admin access control"
```

## Task 6: Device Catalog, Private Images, Sensitive DTOs, and Admin Operations

**Files:**
- Create: `src/DeviceRental.Application/Devices/CreateDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/EditDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/DisableDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/EnableDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/ArchiveDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/RestoreDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Devices/UserDeviceListItem.cs`
- Create: `src/DeviceRental.Application/Devices/AdminDeviceListItem.cs`
- Create: `src/DeviceRental.Infrastructure/Images/SkiaImageProcessor.cs`
- Create: `src/DeviceRental.Infrastructure/Images/LocalPrivateObjectStore.cs`
- Create: `src/DeviceRental.Infrastructure/Images/ImageGarbageCollector.cs`
- Create: `src/DeviceRental.Infrastructure/Options/StorageOptions.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/DeviceConfiguration.cs`
- Generate with EF: migration `DeviceCatalog` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Create: `src/DeviceRental.Infrastructure/Persistence/Queries/DeviceQueries.cs`
- Create: `src/DeviceRental.Web/Pages/Devices/Index.cshtml`
- Create: `src/DeviceRental.Web/Pages/Devices/Index.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Devices/Details.cshtml`
- Create: `src/DeviceRental.Web/Pages/Devices/Details.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Index.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/New.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/New.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Edit.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Edit.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Disable.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Disable.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Enable.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Enable.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Archive.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Archive.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Restore.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Devices/Restore.cshtml.cs`
- Test: `tests/DeviceRental.UnitTests/Devices/ImageValidationTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Devices/DeviceCatalogCommandTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Devices/DeviceCommandConcurrencyTests.cs`
- Test: `tests/DeviceRental.WebTests/Devices/PrivacyResponseTests.cs`
- Test: `tests/DeviceRental.WebTests/Devices/DevicePageTests.cs`

- [ ] **Step 1: Write failing image and DTO tests**

Test JPG/PNG/WebP magic+decode, 5MB, 4096px, 16MP, no SVG/animation, metadata strip/re-encode, staged-orphan cleanup, immutable object keys, and 48h safety beyond unexpired backup manifests.

Seed `SENSITIVE_ADMIN_NOTE`, IMEI, serial, and email. Assert user HTML/HTMX/DTO/image responses never contain them.

Add RED query/input tests for `REQ-DEV-001..004`: authenticated list/detail only; pagination; combined model/asset-code/status/tier search; required asset code/model/image/tier; every optional brand/OS/memory/storage/serial/IMEI/location/notes field round-trips and appears only in its approved DTO; create/edit audit uses field whitelist.

- [ ] **Step 2: Implement SkiaSharp processing and private storage**

Decode with resource limits, re-encode a normalized main image, hash SHA-256, and store in an unexecutable private path. Serve only through authenticated `/devices/{id}/image` with `private, no-store`, `Vary: Cookie`, `nosniff`, and `Referrer-Policy: no-referrer`.

Bind `StorageOptions` with `ValidateOnStart`: Production rejects content-root/static paths and missing runtime mounts. Add `SkiaSharp.NativeAssets.Linux.NoDependencies` and a Linux image-processing smoke test to prove native loading.

- [ ] **Step 3: Write failing device command tests**

Cover create/edit optimistic version conflicts, disable/enable reasons, archive confirmation/reason, reject archive with open loan, and preserve history.

- [ ] **Step 4: Implement device commands and the DeviceCatalog migration**

Implement `CreateDeviceHandler`, `EditDeviceHandler`, `DisableDeviceHandler`, `EnableDeviceHandler`, `ArchiveDeviceHandler`, and `RestoreDeviceHandler` through the interactive executor. Add `DeviceConfiguration` and the `DeviceCatalog` migration with named asset-code, tier/state, pause-reason, image-metadata, and optimistic-version constraints. Run the command and migration tests GREEN.

- [ ] **Step 5: Implement image cleanup and prove backup-manifest safety**

Implement `ImageGarbageCollector`: delete never-referenced staged objects after 24h; delete formerly referenced immutable objects only when absent from the current DB and every unexpired backup manifest plus 48h. A later Worker schedule invokes this service; the behavior itself is GREEN in Task 6.

- [ ] **Step 6: Implement user/admin query separation**

Use distinct `UserDeviceListItem` and `AdminDeviceListItem`; do not load sensitive columns into user projections. One paginated/filterable query result feeds both desktop table and mobile list. Run pagination/search/field round-trip/privacy/audit tests GREEN.

- [ ] **Step 7: Verify and commit**

```powershell
pwsh eng/dotnet.ps1 test tests/DeviceRental.UnitTests --filter Devices
pwsh eng/dotnet.ps1 test tests/DeviceRental.WebTests --filter Devices
git add src/DeviceRental.Application/Devices src/DeviceRental.Infrastructure/Images src/DeviceRental.Infrastructure/Options/StorageOptions.cs src/DeviceRental.Infrastructure/Persistence/Configurations/DeviceConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Migrations src/DeviceRental.Infrastructure/Persistence/Queries/DeviceQueries.cs src/DeviceRental.Web/Pages/Devices src/DeviceRental.Web/Pages/Admin/Devices tests/DeviceRental.UnitTests/Devices tests/DeviceRental.IntegrationTests/Devices tests/DeviceRental.WebTests/Devices docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: add secure device catalog management"
```

## Task 7: Atomic Borrowing and Hot-Device Concurrency

**Files:**
- Create: `src/DeviceRental.Application/Lending/BorrowDeviceCommand.cs`
- Create: `src/DeviceRental.Application/Lending/BorrowDeviceHandler.cs`
- Create: `src/DeviceRental.Application/Lending/BorrowDeviceResult.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Locks/LoanLockQueries.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/LoanConfiguration.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/LoanPolicyVersionConfiguration.cs`
- Generate with EF: migration `LendingAndLoanPolicy` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Create: `src/DeviceRental.Web/Pages/Devices/Borrow.cshtml`
- Create: `src/DeviceRental.Web/Pages/Devices/Borrow.cshtml.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/LoanConstraintsTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/BorrowDeviceTransactionTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/BorrowDeviceConcurrencyTests.cs`
- Test: `tests/DeviceRental.WebTests/Lending/BorrowEndpointConcurrencyTests.cs`

- [ ] **Step 1: Write RED transaction tests**

Assert borrower/due time cannot be supplied by input, default due is effectiveNow + 24h, the policy version is captured, audit + confirmation/pre-due/due outbox messages are in the same transaction, and failure rolls back all rows.

Before migration/constraints exist, write and run RED tests `LoanConstraintsTests.OpenLoanPartialUniqueIndex_AllowsExactlyOnePerDevice`, `ReturnTupleCheck_RejectsAllHalfReturnedShapes`, and `SameBorrower_MayHoldMultipleDifferentDevices`.

- [ ] **Step 2: Write RED PostgreSQL barrier tests**

Run 2/10/50/100 tasks through `AsyncStartGate`. Expected: one success; N-1 named `DEVICE_ALREADY_BORROWED` conflicts; zero unexpected errors/deadlocks; one open loan; one success audit/event set. Also verify one borrower can hold multiple different devices.

- [ ] **Step 3: Implement minimal borrow service**

Create `LoanConfiguration`, `LoanPolicyVersionConfiguration`, and the `LendingAndLoanPolicy` migration. Add `ux_loans_one_open_per_device`, named due/return checks, RESTRICT FKs, and no borrower unique constraint. Extend the Task 4 lock service with the device/open-loan stages in the existing global order.

Then lock actor/device/open loan, check derived availability, create loan using the locked effective time, and map PostgreSQL `23505` only when constraint name is `ux_loans_one_open_per_device`. Run migration, constraint, transaction, and barrier tests GREEN.

- [ ] **Step 4: Implement Web endpoint and conflict feedback**

Require anti-forgery and authenticated active user. On 409, refresh the affected row and show the approved plain-language conflict message plus correlation ID.

- [ ] **Step 5: Verify and commit**

Run focused unit/web tests locally and PG concurrency in CI. Commit:

```powershell
git add src/DeviceRental.Application/Lending/BorrowDeviceCommand.cs src/DeviceRental.Application/Lending/BorrowDeviceHandler.cs src/DeviceRental.Application/Lending/BorrowDeviceResult.cs src/DeviceRental.Infrastructure/Persistence/Locks/LoanLockQueries.cs src/DeviceRental.Infrastructure/Persistence/Configurations/LoanConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Configurations/LoanPolicyVersionConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Migrations src/DeviceRental.Web/Pages/Devices/Borrow.cshtml src/DeviceRental.Web/Pages/Devices/Borrow.cshtml.cs tests/DeviceRental.IntegrationTests/Lending/LoanConstraintsTests.cs tests/DeviceRental.IntegrationTests/Lending/BorrowDeviceTransactionTests.cs tests/DeviceRental.IntegrationTests/Lending/BorrowDeviceConcurrencyTests.cs tests/DeviceRental.WebTests/Lending/BorrowEndpointConcurrencyTests.cs docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: add atomic device borrowing"
```

## Task 8: Returns, Force Return and Disable, Extensions, and Policy Versions

**Files:**
- Create: `src/DeviceRental.Application/Lending/ReturnOwnLoanHandler.cs`
- Create: `src/DeviceRental.Application/Lending/ForceReturnLoanHandler.cs`
- Create: `src/DeviceRental.Application/Lending/ForceReturnAndDisableHandler.cs`
- Create: `src/DeviceRental.Application/Lending/ExtendLoanHandler.cs`
- Create: `src/DeviceRental.Application/Policy/ChangeLoanPolicyHandler.cs`
- Create: `src/DeviceRental.Web/Pages/Loans/Return.cshtml`
- Create: `src/DeviceRental.Web/Pages/Loans/Return.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/Index.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/Index.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/Extend.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/Extend.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/ForceReturn.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/ForceReturn.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/ForceReturnAndDisable.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Loans/ForceReturnAndDisable.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Settings/LoanPolicy.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Settings/LoanPolicy.cshtml.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/LoanExtensionConfiguration.cs`
- Generate with EF: migration `LoanExtensionsAndPolicyHistory` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Test: `tests/DeviceRental.IntegrationTests/Lending/ReturnAuthorizationTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/ReturnConcurrencyTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/ForceReturnAndDisableTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/LoanExtensionTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/PolicyVersionConcurrencyTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Lending/LoanHistoryQueryTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/SelfReturnNotificationTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/ForceReturnNotificationTests.cs`

- [ ] **Step 1: Write failing authorization and return-tuple tests**

Current borrower (including an administrator borrowing personally) uses SELF without reason. Other users are denied. Admin force return requires a reason and uses FORCED. Half-returned tuples must fail at the DB constraint.

Add RED tests for `REQ-LOAN-006/009/010/014`: ordinary device queries show current borrower's real name and due time but no email; ordinary/forced return derives Available while force-return-and-disable derives Unavailable; no endpoint/handler supports direct borrower transfer; users see only their current/history records while admins can search all including archived devices and disabled borrowers.

Add RED event tests: `SelfReturnNotificationTests.ReturnOwn_QueuesReturnConfirmationAndCancelsPendingReminders` covers `REQ-NOTIFY-007`; `ForceReturnNotificationTests.ForceReturn_QueuesReasonedNotificationForOriginalBorrower` covers `REQ-NOTIFY-003`.

- [ ] **Step 2: Write failing no-available-window tests**

Pause `ForceReturnAndDisable` before commit. Concurrent readers see old Borrowed; concurrent borrow blocks; after commit all see Unavailable. Never expose Available.

- [ ] **Step 3: Write failing extension/policy tests**

Cover later-of-old-due/effective-now base, 60m/7d limits, reason, independent extension history, policy changes affecting only new loans, and reminder cancellation/replacement.

- [ ] **Step 4: Implement the extension migration and application services**

Create `LoanExtensionConfiguration` and `LoanExtensionsAndPolicyHistory` migration, including 60..10080 minute/new-due checks and RESTRICT FKs. Implement separate services `ReturnOwn`, `ForceReturn`, `ForceReturnAndDisable`, `Extend`, and `ChangeLoanPolicy`; queue the exact domain notification events asserted in Step 1. Run authorization, tuple, notification-event, policy, and concurrency tests GREEN.

Implement `LoanHistoryQueryService` with separate user/admin projections and no transfer command. Run borrower-display, derived-return-status, no-transfer surface, and history authorization/search tests GREEN.

- [ ] **Step 5: Implement Razor confirmation dialogs**

Use separate commands `ReturnOwn`, `ForceReturn`, `ForceReturnAndDisable`, and `Extend`; no generic status setter. GET confirmation pages work without JavaScript; HTMX requests render a `<dialog>` partial.

- [ ] **Step 6: Verify and commit**

```powershell
git add src/DeviceRental.Application/Lending/ReturnOwnLoanHandler.cs src/DeviceRental.Application/Lending/ForceReturnLoanHandler.cs src/DeviceRental.Application/Lending/ForceReturnAndDisableHandler.cs src/DeviceRental.Application/Lending/ExtendLoanHandler.cs src/DeviceRental.Application/Policy/ChangeLoanPolicyHandler.cs src/DeviceRental.Infrastructure/Persistence/Configurations/LoanExtensionConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Migrations src/DeviceRental.Web/Pages/Loans src/DeviceRental.Web/Pages/Admin/Loans src/DeviceRental.Web/Pages/Admin/Settings tests/DeviceRental.IntegrationTests/Lending tests/DeviceRental.IntegrationTests/Notifications/SelfReturnNotificationTests.cs tests/DeviceRental.IntegrationTests/Notifications/ForceReturnNotificationTests.cs docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: add return extension and loan policy flows"
```

## Task 9: Notification Outbox Worker and Delivery State Machine

**Files:**
- Create: `src/DeviceRental.Application/Notifications/IOutboxProcessor.cs`
- Create: `src/DeviceRental.Application/Notifications/INotificationSender.cs`
- Create: `src/DeviceRental.Application/Notifications/OutboxProcessor.cs`
- Create: `src/DeviceRental.Infrastructure/Notifications/PostgresOutboxStore.cs`
- Create: `src/DeviceRental.Infrastructure/Notifications/SmtpNotificationSender.cs`
- Create: `src/DeviceRental.Infrastructure/Notifications/NotificationTemplateRenderer.cs`
- Create: `src/DeviceRental.Infrastructure/Options/SmtpOptions.cs`
- Create: `src/DeviceRental.Infrastructure/Options/WorkerOptions.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/NotificationDeliveryConfiguration.cs`
- Generate with EF: migration `NotificationDeliveryAndOperationalIndexes` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Create: `src/DeviceRental.Worker/Program.cs`
- Create: `src/DeviceRental.Worker/OutboxWorker.cs`
- Create: `src/DeviceRental.Worker/OutboxRecoveryWorker.cs`
- Create: `src/DeviceRental.Worker/ImageCleanupWorker.cs`
- Create: `src/DeviceRental.Worker/WorkerHeartbeatService.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Notifications/Failed.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Notifications/Failed.cshtml.cs`
- Test: `tests/DeviceRental.UnitTests/Notifications/NotificationTemplateTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/OutboxClaimConcurrencyTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/OutboxSendingCasTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/OutboxRecoveryTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Notifications/WorkerLifecycleTests.cs`

- [ ] **Step 1: Write failing claim/lease tests**

Two workers claim due rows with `FOR UPDATE SKIP LOCKED`; every row is claimed once. Expired workers cannot complete using an old lease.

Before the delivery table/migration exists, write RED tests for delivery dedupe uniqueness, valid state tuples, due/lease partial indexes, and PG18 migration upgrade from the Task 8 schema.

- [ ] **Step 2: Write failing send-CAS tests**

Before `CLAIMED -> SENDING`, atomically compare lease, aggregate version, expected due, and returned_at. Aggregate change cancels. After SENDING, at most one message may be accepted. AcceptanceUnknown becomes REVIEW_REQUIRED and is not retried.

Add RED template tests for every approved event: borrow confirmation (`REQ-NOTIFY-001`), pre-due/due (`REQ-NOTIFY-002`), force return with reason (`REQ-NOTIFY-003`), extension with new due time, self return confirmation (`REQ-NOTIFY-007`), and failure masking (`REQ-NOTIFY-005`).

Add RED scheduling tests proving reminders and overdue derivation continue during `[19:00, next 09:00)` (`REQ-NOTIFY-006`), extension sends the new due time immediately, and old PENDING/CLAIMED reminders follow the exact Task 8 cancellation/CAS contract.

- [ ] **Step 3: Implement notification persistence and migration**

Create `NotificationDeliveryConfiguration` and `NotificationDeliveryAndOperationalIndexes` migration. Add delivery dedupe/state checks, PENDING due index, CLAIMED lease index, and migration upgrade tests. Run database RED tests GREEN.

- [ ] **Step 4: Implement templates, claim, send, and failure classification**

Commit the claim before SMTP. Prove with a blocked sender that another connection is not blocked by a held database transaction. Retry only explicit TransientNotAccepted; dead-letter PermanentRejected; manual-review unknown acceptance.

Bind `SmtpOptions`/`WorkerOptions` with `ValidateOnStart`; Production rejects localhost/demo recipients, missing TLS/credentials, invalid lease/timeout intervals, and disabled heartbeat. Tests use a programmable sender; no test sends external email.

- [ ] **Step 5: Write failing recovery and lifecycle tests**

Cover graceful cancellation, forced termination after CLAIM and after SENDING, lease takeover, startup recovery, and stale SENDING -> REVIEW_REQUIRED without resend. Verify `OutboxWorker` does not hold a DB transaction during SMTP. Verify `ImageCleanupWorker` calls Task 6's tested collector and honors shutdown. Verify heartbeat/readiness becomes unhealthy when the Worker stalls.

- [ ] **Step 6: Implement Worker composition root, reaper, cleanup schedule, and health**

`Program.cs` wires OutboxWorker, OutboxRecoveryWorker, ImageCleanupWorker, options validation, structured logs, heartbeat, and graceful stop. Recovery marks acceptance-unknown SENDING rows REVIEW_REQUIRED; it never returns them to PENDING automatically.

- [ ] **Step 7: Implement failed/review admin page and retention**

Show masked recipients, sanitized errors, event/object/correlation IDs, attempts, and explicit no-auto-resend text. Apply 30-day processed and 90-day failure/review retention policies.

- [ ] **Step 8: Verify and commit**

```powershell
git add src/DeviceRental.Application/Notifications src/DeviceRental.Infrastructure/Notifications src/DeviceRental.Infrastructure/Options/SmtpOptions.cs src/DeviceRental.Infrastructure/Options/WorkerOptions.cs src/DeviceRental.Infrastructure/Persistence/Configurations/NotificationDeliveryConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Migrations src/DeviceRental.Worker src/DeviceRental.Web/Pages/Admin/Notifications tests/DeviceRental.UnitTests/Notifications/NotificationTemplateTests.cs tests/DeviceRental.IntegrationTests/Notifications docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: add reliable notification outbox worker"
```

## Task 10: Responsive Razor UI Matching the Approved Preview

**Files:**
- Create: `src/DeviceRental.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/DeviceRental.Web/Pages/Shared/_AuthLayout.cshtml`
- Create: `src/DeviceRental.Web/Pages/Shared/_ClosedLayout.cshtml`
- Create: `src/DeviceRental.Web/Pages/Shared/_AppHeader.cshtml`
- Create: `src/DeviceRental.Web/Pages/Shared/_FeedbackRegion.cshtml`
- Create: `src/DeviceRental.Web/Pages/Shared/_ModalHost.cshtml`
- Create: `src/DeviceRental.Web/Pages/My/Loans/Index.cshtml`
- Create: `src/DeviceRental.Web/Pages/My/Loans/Index.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Admin/Audit/Index.cshtml`
- Create: `src/DeviceRental.Web/Pages/Admin/Audit/Index.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Closed.cshtml`
- Create: `src/DeviceRental.Web/Pages/Closed.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Error/403.cshtml`
- Create: `src/DeviceRental.Web/Pages/Error/404.cshtml`
- Create: `src/DeviceRental.Web/Pages/Error/500.cshtml`
- Create: `src/DeviceRental.Web/ViewComponents/PrimaryNavigationViewComponent.cs`
- Create: `src/DeviceRental.Web/TagHelpers/UiIconTagHelper.cs`
- Create: `src/DeviceRental.Web/TagHelpers/StatusBadgeTagHelper.cs`
- Create: `src/DeviceRental.Web/Styles/tailwind.input.css`
- Create: `src/DeviceRental.Web/wwwroot/css/app.css`
- Create: `src/DeviceRental.Web/wwwroot/js/dialog.js`
- Create: `src/DeviceRental.Web/wwwroot/js/htmx-feedback.js`
- Create: `src/DeviceRental.Web/wwwroot/js/mobile-navigation.js`
- Create: `src/DeviceRental.Web/wwwroot/vendor/htmx/htmx.min.js`
- Create: `src/DeviceRental.Web/wwwroot/vendor/lucide/lucide.svg`
- Create: `src/DeviceRental.Application/Audit/IAuditQueryService.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Queries/AuditQueries.cs`
- Test: `tests/DeviceRental.WebTests/Presentation/NavigationAuthorizationTests.cs`
- Test: `tests/DeviceRental.WebTests/Presentation/PrivacyCacheHeaderTests.cs`
- Test: `tests/DeviceRental.WebTests/Presentation/ApprovedPageSurfaceTests.cs`
- Test: `tests/DeviceRental.E2ETests/UserBorrowReturnJourneyTests.cs`
- Test: `tests/DeviceRental.E2ETests/ResponsiveDeviceListTests.cs`
- Test: `tests/DeviceRental.E2ETests/KeyboardJourneyTests.cs`
- Test: `tests/DeviceRental.E2ETests/AccessibilitySmokeTests.cs`
- Test: `tests/DeviceRental.E2ETests/ApprovedScreenshotTests.cs`
- Create: `eng/browser-versions.json`
- Test: `tests/DeviceRental.WebTests/Audit/AuditAuthorizationTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Audit/ArchivedHistoryQueryTests.cs`

- [ ] **Step 1: Write failing navigation/privacy/cache Web tests**

Assert ordinary users see only Devices/My Loans; admins receive management navigation; direct `/admin/*` is denied; user responses do not include sensitive sentinels; protected HTML/HTMX/images use private no-store headers.

`ApprovedPageSurfaceTests` must RED until these approved routes exist: account pages (D01), devices/list/detail (D02/D03), My Loans (D04), Admin Devices/form (D05/D06), Admin Loans (D07), Loan Policy (D09), Admin Audit (D10), Failed Notifications (D11), Closed 503 (D12), and error feedback routes/states (D13). D08 and D13 remain test-only screenshot boards composed from real dialogs/errors, not production routes.

Add RED `AuditAuthorizationTests` and `ArchivedHistoryQueryTests`: ordinary users cannot read audit/all-user history; admins can filter by time/actor/object/event, including archived devices and disabled accounts; audit rows expose only whitelisted changed fields/reasons/correlation IDs and have no product update/delete route.

- [ ] **Step 2: Implement layouts and reusable partials**

Create `_Layout`, `_AuthLayout`, `_ClosedLayout`, navigation partials, desktop/mobile result partials, status/icon tag helpers, feedback region, modal host, and loading indicator. Use the approved semantic tokens, not the preview's fake data renderer.

Implement `AuditQueries`/Admin Audit Page with the RED filters and immutable read model. Run audit authorization/history tests GREEN.

Run `npm ci` and `npm run build:css` using pinned Tailwind 4.3.3. Vendor exact HTMX 2.0.10 and Lucide 1.37.0 from `package-lock.json`; record licenses and file hashes. Never load CDN assets at runtime.

- [ ] **Step 3: Implement progressive enhancement**

Filters are native GET forms plus targeted `hx-get` with 300ms delay and URL synchronization. Commands are normal anti-forgery POST forms; HTMX is an enhancement only. Do not enable global `hx-boost`.

- [ ] **Step 4: Implement accessible dialogs and feedback**

Use native `<dialog>` with focus return. Field errors use `asp-validation-for`; live regions announce success/errors; 401/403/409/429/503/unknown errors use approved recovery actions.

- [ ] **Step 5: Run responsive and keyboard E2E tests**

Use deterministic synthetic seeds, fixed `FakeTimeProvider`, system fonts, and `deviceScaleFactor: 1`. At 320 CSS px/400% reflow, 360×800, 390×844, 768×1024, 900/901 breakpoints, 1366×768, 1440×1024, and 1920×1080 assert no horizontal overflow, one accessible result representation, 44px touch actions, proper dialog focus/Escape/return, disabled JS fallback, and exact screenshots D01-D07/D09-D12 plus M01-M07. Generate D08 by opening each real dialog and D13 by inducing each real error.

Run axe on login, device list/detail, forms, mobile lists, and dialogs; require zero unaccepted violations at any impact level. Run current and previous-major Chrome/Edge from exact-version, hash-verified artifacts on a controlled/self-hosted Windows runner recorded in `eng/browser-versions.json`; do not claim coverage from an unpinned latest runner. NVDA login/borrow/return/admin-dialog journeys are required signed UAT items before release readiness.

- [ ] **Step 6: Verify and commit**

```powershell
git add package.json package-lock.json eng/browser-versions.json src/DeviceRental.Application/Audit src/DeviceRental.Infrastructure/Persistence/Queries/AuditQueries.cs src/DeviceRental.Web tests/DeviceRental.WebTests tests/DeviceRental.E2ETests docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "feat: implement responsive device desk UI"
```

## Task 11: Health, Observability, Retention, Security Headers, and Operations

**Files:**
- Create: `src/DeviceRental.Web/Middleware/SecurityHeadersMiddleware.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/OperationalMetrics.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/SecurityEventMonitor.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/AvailabilitySliCalculator.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/BackupFreshnessMonitor.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/ClockDriftMonitor.cs`
- Create: `src/DeviceRental.Infrastructure/Operations/MaintenanceCoordinator.cs`
- Create: `src/DeviceRental.Domain/Notifications/RecoveryBatch.cs`
- Create: `src/DeviceRental.Infrastructure/Persistence/Configurations/RecoveryBatchConfiguration.cs`
- Generate with EF: migration `RecoveryBatchesAndRetention` under `src/DeviceRental.Infrastructure/Persistence/Migrations/`
- Create: `src/DeviceRental.Worker/RetentionWorker.cs`
- Modify: `src/DeviceRental.Infrastructure/DependencyInjection.cs`
- Modify: `src/DeviceRental.Web/Program.cs`
- Modify: `src/DeviceRental.Worker/Program.cs`
- Create: `src/DeviceRental.Web/Dockerfile`
- Create: `src/DeviceRental.Worker/Dockerfile`
- Create: `deploy/.dockerignore`
- Create: `deploy/compose.yaml`
- Create: `deploy/compose.production.yaml`
- Create: `scripts/backup.ps1`
- Create: `scripts/restore.ps1`
- Create: `scripts/backup.sh`
- Create: `scripts/restore.sh`
- Create: `performance/k6/hot-device.js`
- Create: `performance/k6/operations-slo.js`
- Create: `performance/k6/opening-burst.js`
- Create: `performance/k6/outbox-backlog.js`
- Create: `performance/k6/image-upload.js`
- Test: `tests/DeviceRental.WebTests/Operations/SecurityHeadersTests.cs`
- Test: `tests/DeviceRental.WebTests/Operations/HealthAndReadinessTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/SecurityEventAlertTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/AvailabilitySliTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/PerformanceSloSmokeTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/OperationalMetricsTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/BackupFreshnessAlertTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/ClockDriftAlertTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/RetentionPolicyTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/BackupRestoreRehearsalTests.cs`
- Test: `tests/DeviceRental.IntegrationTests/Operations/LinuxImageSmokeTests.cs`

- [ ] **Step 1: Write failing health/security/metrics tests**

Verify health/readiness remain available outside hours, business pages return readable 503, HSTS/CSP/CSRF/cache headers are present, and metrics expose request/5xx/P95, DB pool/lock wait, borrow conflict, outbox age, SMTP failure, disk/certificate/time drift, and backup freshness.

Add RED `SecurityEventAlertTests` for sensitive administrator actions and repeated authorization failures (`NFR-SEC-005`). Add RED `AvailabilitySliTests` for the exact open-window denominator and >=99.5% target (`NFR-AVL-001`). Add RED `PerformanceSloSmokeTests`/k6 assertions for list/detail P95 <=1s, borrow/return P95 <=2s, technical error rate <1%, and expected 409 exclusion (`NFR-PERF-001`).

- [ ] **Step 2: Implement security events, availability SLI, metrics, and alert thresholds**

Use 2y audit, 90d app log, 30d processed payload, 90d failure/review, and 35d backups; warn at 20h and alert at 24h since backup; alert at >2s app/DB clock drift.

Implement the RED tests GREEN. Security events use whitelisted fields and correlation IDs; SLI excludes planned closed hours but counts unapproved maintenance; metrics separate expected 409 business conflicts from technical errors.

Register SecurityHeadersMiddleware, health/readiness, metrics, security/availability/backup/clock monitors in Web `Program.cs`/Infrastructure `DependencyInjection.cs`. Register RetentionWorker and operational monitors in Worker `Program.cs`; tests resolve the production service provider and assert every hosted service/middleware/options validator is present, not merely constructible in isolation.

- [ ] **Step 3: Write failing recovery-batch and N/N-1 migration tests**

Before the recovery entity/migration exists, RED tests require: migration from Task 9 schema to current and rollback/forward recovery; persisted recovery batch with source checkpoint/object manifest hash; restored pending messages assigned to exactly one batch; open/overdue/self-return/force-return/extension sample records preserve invariants.

- [ ] **Step 4: Implement recovery persistence and backup/restore scripts**

Choose one strategy: `MaintenanceCoordinator` opens a dedicated Npgsql connection and acquires the exclusive session-level `MAINTENANCE_LOCK_KEY` used by Task 4. Existing shared interactive locks drain; new interactive writes block. While keeping that same connection alive, the coordinator records the PostgreSQL checkpoint/object-manifest hash and launches the dump/object/key backup child processes. It releases the exclusive lock only after all backup artifacts and manifest hashes are durable. Back up PostgreSQL, objects, Data Protection keys, migration version, and config references. Restore with Worker stopped; create a persisted `RecoveryBatch` and assign restored pending messages for manual replay/cancel decisions.

Provide equivalent PowerShell and POSIX entry points that invoke the AdminCli/`MaintenanceCoordinator`; shell scripts never try to emulate the session lock with separate `psql` and `pg_dump` sessions. Linux containers do not assume Windows PowerShell. `BackupRestoreRehearsalTests` holds the exclusive lock while launching a competing interactive command and proves that command cannot commit between checkpoint and dump. It then creates open/overdue/self-return/force-return/extension/notification/image data, upgrades, backs up, restores into an isolated PG18 database/object root, forward-recovers to current, and verifies RPO/RTO evidence and all invariants.

- [ ] **Step 5: Add k6 hot-device scenario**

Write the performance scripts RED against explicit thresholds, then run after implementation:

```text
operations-slo.js   5m warmup + 15m steady; list/detail P95<=1s; borrow/return P95<=2s; technical errors<1%; expected 409 excluded
opening-burst.js    60s ramp to 100 users; login/list/borrow=20/60/20; non-hot lock wait P95<500ms; 5xx<1%
hot-device.js       100 requests at one barrier x100 rounds; exactly 1 success/round; 99 expected 409; zero 5xx/deadlocks
outbox-backlog.js   seed 10,000 due messages; sender capped 10/s; clear <=30m; Web P95 degradation <=20%; no lease overwrite
image-upload.js     20 concurrent 5MB/16MP-valid images for 15m; CPU P95<80%; pool<80%; memory growth<10%; oversized reject <=1s
```

Run k6 1.8.1 using the digest in `eng/container-images.json`. Capture app/DB/worker CPU, memory, pool, lock waits, and outbox-age time series. These scenarios are all `[M]`; hot-device does not replace the other four or the integration barrier tests.

- [ ] **Step 6: Build and smoke-test Linux containers**

Use multi-stage .NET 10 images pinned by digest, restore locked packages, run as a non-root UID, use read-only root filesystems where practical, mount uploads/keys/config separately, include SkiaSharp Linux native assets, and expose only Web. `LinuxImageSmokeTests` decodes/re-encodes a JPEG/WebP, starts Web/Worker against PostgreSQL 18, verifies readiness, graceful SIGTERM, and no root process.

- [ ] **Step 7: Verify and commit**

```powershell
git add src/DeviceRental.Web/Middleware/SecurityHeadersMiddleware.cs src/DeviceRental.Infrastructure/Operations src/DeviceRental.Domain/Notifications/RecoveryBatch.cs src/DeviceRental.Infrastructure/Persistence/Configurations/RecoveryBatchConfiguration.cs src/DeviceRental.Infrastructure/Persistence/Migrations src/DeviceRental.Infrastructure/DependencyInjection.cs src/DeviceRental.Web/Program.cs src/DeviceRental.Worker/Program.cs src/DeviceRental.Worker/RetentionWorker.cs src/DeviceRental.AdminCli/Program.cs src/DeviceRental.Web/Dockerfile src/DeviceRental.Worker/Dockerfile deploy scripts performance tests/DeviceRental.WebTests/Operations tests/DeviceRental.IntegrationTests/Operations
git commit -m "feat: add operational security and recovery tooling"
```

## Task 12: Full CI, Documentation, Release Readiness, and Remote Delivery

**Files:**
- Modify: `.github/workflows/ci.yml`
- Create: `docs/operations/deployment.md`
- Create: `docs/operations/backup-restore.md`
- Create: `docs/operations/configuration.md`
- Create: `docs/traceability.md`
- Create: `docs/architecture/threat-model.md`
- Create: `docs/architecture/data-dictionary.md`
- Create: `docs/architecture/adr-index.md`
- Create: `docs/uat-checklist.md`
- Create: `eng/Verify-Traceability.ps1`
- Create: `eng/Verify-NoSkippedMvpTests.ps1`
- Create: `eng/Verify-ProductionConfiguration.ps1`
- Modify: `README.md`

- [ ] **Step 1: Complete the CI graph**

Jobs:

```text
build-unit        locked restore, build, format, unit, TRX, XPlat coverage
integration       digest-pinned PostgreSQL 18, migrations, constraints, transactions, concurrency, outbox
web               PostgreSQL 18, real Identity/cookies/CSRF, Web tests
e2e-smoke         Kestrel + PostgreSQL 18, Playwright Chromium/Chrome/Edge, axe, screenshots/traces
security          CodeQL/SAST, secret scan, dependency vulnerability/deprecation, license report, authenticated DAST, generated role/state/action authorization matrix
container         Web/Worker image build, SBOM, vulnerability scan, non-root/Linux/Skia smoke
performance       k6 SLO and 100x100 hot-device scenario on workflow_dispatch/schedule/release
recovery          isolated backup/restore rehearsal on workflow_dispatch/schedule/release
```

Database jobs use digest-pinned PostgreSQL 18, Mailpit, and private local-object-store mounts where applicable. Use locked NuGet/npm restore, migrations, TRX/coverage upload, and failure artifacts. Pin checkout/setup-dotnet/upload-artifact/CodeQL action SHAs from their first introduction; never use a floating action tag in the committed workflow.

- [ ] **Step 2: Generate executable traceability evidence**

`eng/Verify-Traceability.ps1` compares the 70 approved Markdown IDs, the 70 `docs/traceability.csv` rows, and test `[Trait("Requirement", "...")]` values. Fail on missing/duplicate/unknown IDs, missing test names, or non-passing status in release evidence.

`eng/Verify-NoSkippedMvpTests.ps1` has two explicit modes. `-Mode Structure` compares all 72 IDs parsed from `[M]` rows in `docs/test-plan.md`, all 72 unique `docs/mvp-test-cases.csv` rows, and compiled test methods carrying `[Trait("MvpCase", "...")]`; it fails missing/duplicate/unknown owners but does not claim execution. `-Mode Execution` additionally requires CI to download and aggregate TRX from every test job and fails any missing/not-discovered/failed/skipped/inconclusive case, duplicate primary owner, unknown case, missing TRX job, or CSV status other than `Passing`. A count-only or skipped-only check is insufficient.

Commit and push the complete workflow and gate scripts before attempting Step 4:

```powershell
git add .github/workflows/ci.yml eng/Verify-Traceability.ps1 eng/Verify-NoSkippedMvpTests.ps1 eng/Verify-ProductionConfiguration.ps1 docs/traceability.csv docs/mvp-test-cases.csv
git commit -m "ci: enforce full quality gates"
git push
```

Expected: the newly complete CI graph is visible on the feature branch and can be dispatched repeatedly.

- [ ] **Step 3: Run local verification**

```powershell
pwsh eng/dotnet.ps1 restore --locked-mode
npm ci
npm run build:css
pwsh eng/dotnet.ps1 build --configuration Release --no-restore
pwsh eng/dotnet.ps1 test tests/DeviceRental.UnitTests --configuration Release --no-build --logger trx --collect "XPlat Code Coverage"
pwsh eng/dotnet.ps1 test tests/DeviceRental.WebTests --configuration Release --no-build --logger trx --collect "XPlat Code Coverage"
pwsh eng/dotnet.ps1 format --verify-no-changes
pwsh eng/Verify-Traceability.ps1
pwsh eng/Verify-NoSkippedMvpTests.ps1 -Mode Structure
```

Expected: clean output, zero failures/warnings.

- [ ] **Step 4: Run feature-branch PostgreSQL/CI verification three times**

Run the complete feature-branch CI workflow three consecutive times with no automatic retries. Confirm zero skipped `[M]` tests, all migration/constraint/concurrency/outbox/E2E/security/container checks green, and uploaded evidence complete. The approved release process still requires a later user-approved merge and three fresh `main` CI runs; feature-branch success is not a substitute.

Each full workflow has an aggregate gate that downloads every TRX artifact and runs `pwsh eng/Verify-NoSkippedMvpTests.ps1 -Mode Execution -TrxPath <aggregate-directory>`; the workflow cannot be green on structural evidence alone.

- [ ] **Step 5: Perform security and dependency checks**

```powershell
pwsh eng/dotnet.ps1 list package --vulnerable --include-transitive
pwsh eng/dotnet.ps1 list package --deprecated
pwsh eng/dotnet.ps1 format --verify-no-changes
```

Review licenses, container image scan, secret scan, and production configuration validation.

Run `eng/Verify-ProductionConfiguration.ps1` against deliberately invalid configurations (demo domain, absent SMTP/storage/DB/Data Protection secret, untrusted Host/proxy, non-PG18) and one secret-injected synthetic valid configuration. The repository and images must contain no real secret values.

- [ ] **Step 6: Perform restore rehearsal and browser QA**

Restore an isolated backup within approved RPO/RTO; verify users, images, open-loan uniqueness, audit continuity, Data Protection policy, and controlled outbox replay. Run desktop/mobile screenshots and pixel checks against the approved preview.

Execute `docs/uat-checklist.md` with business-owner, test-admin, security, and operations sign-off fields, including the four NVDA journeys and current/previous Chrome/Edge matrix. If any signature/evidence is absent, the feature branch may be handed off as `Development Complete - NOT READY FOR RELEASE`; Task 12 must not claim `Release Ready`.

- [ ] **Step 7: Final code review and commit**

```powershell
git add .github/workflows/ci.yml eng/Verify-Traceability.ps1 eng/Verify-NoSkippedMvpTests.ps1 eng/Verify-ProductionConfiguration.ps1 docs/operations docs/architecture docs/traceability.md docs/traceability.csv docs/uat-checklist.md README.md
git commit -m "docs: complete implementation and operations handoff"
git status --short --branch
```

- [ ] **Step 8: Push the feature branch**

```powershell
git push -u origin codex/mobile-device-rental-system
```

The user's 2026-09-01 confirmation authorizes this feature-branch push to the supplied repository. It does not authorize merging to `main`, three post-merge release attestations, production deployment, permission changes, or real-data import.

## Completion Evidence Required

- Approved v1.0 requirement/design/test documents and preview links remain valid.
- `dotnet build -c Release` passes with zero warnings.
- Unit, PostgreSQL integration, Web, and E2E test reports pass; no `[M]` skips.
- PG18 constraint and concurrency evidence proves one open loan per device and no transient Available state.
- Access-window, authorization-version, sensitive-field, image, outbox, and recovery tests pass.
- 70/70 requirement IDs map to named passing tests.
- Browser screenshots match the approved desktop/mobile design and pass 320px reflow, keyboard, focus, target-size, contrast, and axe gates.
- Restore rehearsal meets approved RPO/RTO.
- Business/test-admin/security/operations UAT signatures and NVDA/browser-matrix evidence are present for `Release Ready`; otherwise the handoff explicitly says `NOT READY FOR RELEASE`.
- Feature branch is pushed; production remains untouched.
