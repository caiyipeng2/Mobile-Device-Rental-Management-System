# Mobile Device Rental Management System

Internal mobile-device inventory and one-day lending system for a company test group. The approved MVP supports company-email accounts, role-aware device management, atomic borrowing and returning, audited administration, an interactive service window of 09:00-19:00 Asia/Shanghai, and reliable notification processing.

## Current status

Development is in progress on `codex/mobile-device-rental-system`. The repository contains the approved specifications, responsive interface preview, .NET 10 modular monolith, identity application service, PostgreSQL identity/audit/outbox/device/loan migrations, transactional EF device/loan persistence, bounded JPEG/PNG/WebP validation, private filesystem image storage, and a clickable Razor Pages device desk. Configure `Storage:DeviceImageRoot` to a dedicated path outside the Web content root; the Web MVP still uses a process-local adapter for its rendered demo pages and is not production-ready until that adapter is replaced, stored image metadata is linked transactionally to device records, email verification/password recovery, and Worker notifications are wired.

- [Requirements specification](docs/requirements-specification.md)
- [Development design](docs/development-design.md)
- [Test plan](docs/test-plan.md)
- [Implementation plan](docs/superpowers/plans/2026-09-01-mobile-device-rental-system.md)
- [Interactive UI preview](docs/ui-preview/index.html)
- [Rendered desktop preview](docs/ui-preview/device-desk-desktop.png)
- [Rendered mobile preview](docs/ui-preview/device-desk-mobile.png)
- [Requirement traceability](docs/traceability.csv)
- [MVP test registry](docs/mvp-test-cases.csv)

## Architecture

The solution is a .NET 10 modular monolith backed only by PostgreSQL 18.

```text
Domain <- Application <- Infrastructure
                         ^       ^
                         |       |
                  Web / Worker / AdminCli
```

`Web`, `Worker`, and `AdminCli` are independent composition roots. They share application and infrastructure services but do not reference one another. Tests enforce the complete project-reference graph.

## Prerequisites

- PowerShell 7 (`pwsh`)
- Git
- Node.js 24 or newer and npm 11 or newer
- PostgreSQL 18, or Docker for database integration tests
- .NET SDK 10.0.400 and .NET/ASP.NET Core runtime 10.0.11

Install the repository-local .NET SDK when needed:

```powershell
pwsh eng/Bootstrap-DotNet.ps1
```

The bootstrap script downloads Microsoft's installer, requires a valid Microsoft Authenticode signature, and installs into the ignored main-checkout `.tools/dotnet` directory. It does not modify the user or system `PATH`.

Every repository .NET command goes through `eng/dotnet.ps1`, which resolves that shared SDK from the Git common directory and isolates CLI/NuGet caches beneath `.tools`.

## Build and unit checks

```powershell
pwsh eng/Verify-Prerequisites.ps1 -Mode LocalUnit
pwsh eng/Verify-CiSkeleton.ps1
npm ci
npm run build
pwsh eng/dotnet.ps1 restore --locked-mode
pwsh eng/dotnet.ps1 build --configuration Release --no-restore
pwsh eng/dotnet.ps1 test --project tests/DeviceRental.UnitTests/DeviceRental.UnitTests.csproj --configuration Release --no-build
```

`LocalUnit` deliberately allows PostgreSQL to be absent and reports that database tests are unavailable. `Database` mode requires either a reachable PostgreSQL 18 admin connection in `DEVICERENTAL_TEST_POSTGRES_ADMIN` or a working Docker daemon. When a connection string is provided, the check executes `SHOW server_version_num` and rejects any non-18 server.

On Windows builds without complete CET support, the .NET 10 Roslyn apphost can stop with `Your Windows doesn't fully support CET`. Install all available Windows updates before local compilation. The Linux GitHub Actions jobs remain the authoritative clean-host build and PostgreSQL 18 execution environment; this repository does not disable system exploit protections.

## Deterministic dependencies

- NuGet versions are centralized in `Directory.Packages.props`; every project commits `packages.lock.json`.
- npm dependencies use exact versions and a committed `package-lock.json`; CI uses `npm ci`.
- Frontend build vendors htmx and Lucide with their licenses and compiles Tailwind from `src/DeviceRental.Web/Styles/tailwind.input.css`.
- Container tags and registry manifest digests are recorded in `eng/container-images.json`.
- GitHub Actions are referenced by full commit SHA. `eng/Verify-CiSkeleton.ps1` rejects floating actions and tag-only or `latest` container references.

## Configuration and secrets

Do not commit company domains, employee data, administrator addresses, SMTP credentials, storage credentials, Data Protection keys, or production connection strings. Production values are supplied through environment variables or a secret provider. Local-only overrides belong in ignored `appsettings.Local.json` or `.env` files.

## Test policy

PostgreSQL behavior and concurrency are tested against PostgreSQL 18. SQLite and EF InMemory are not accepted substitutes. Every approved behavior test carries a requirement trait, and each MVP test case has exactly one primary owner. The two CSV registries start as `Planned` and are promoted to `Implemented` or `Passing` as their owning tasks land.
