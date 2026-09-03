# Account Verification And Password Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add production account email-verification and password-reset workflows with bounded Identity tokens while preserving generic responses for unknown accounts.

**Architecture:** Extend the application account service with token request/consume operations. The Infrastructure adapter uses ASP.NET Core Identity token providers backed by the existing `user_tokens` table, while Razor Pages render generic request results and only activate accounts after a valid, unexpired token is consumed.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core/Npgsql, Razor Pages, xUnit v3/Microsoft Testing Platform.

---

### Task 1: Application Contracts And RED Tests

**Files:**
- Modify: `src/DeviceRental.Application/Identity/IAccountStore.cs`
- Modify: `src/DeviceRental.Application/Identity/IAccountApplicationService.cs`
- Modify: `tests/DeviceRental.UnitTests/Identity/AccountApplicationServiceTests.cs`

- [x] Add token request/confirmation result records and failing tests for valid confirmation, invalid confirmation, generic reset requests, and password validation.
- [x] Run the focused unit tests and confirm they fail because the new operations do not exist.

### Task 2: Identity Token Provider And Store

**Files:**
- Modify: `src/DeviceRental.Infrastructure/Identity/IdentityAccountStore.cs`
- Create: `src/DeviceRental.Infrastructure/Identity/AccountTokenProviders.cs`
- Modify: `src/DeviceRental.Web/Program.cs`

- [x] Register separate 24-hour email-confirmation and 30-minute password-reset providers.
- [x] Implement Identity token generation, confirmation, reset, security-stamp rotation, and generic unknown-account handling through `IAccountStore`.
- [x] Run the focused unit tests and infrastructure build.

### Task 3: Razor Account Flows

**Files:**
- Create: `src/DeviceRental.Web/Pages/Account/VerifyEmail.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/VerifyEmail.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/ForgotPassword.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/ForgotPassword.cshtml.cs`
- Create: `src/DeviceRental.Web/Pages/Account/ResetPassword.cshtml`
- Create: `src/DeviceRental.Web/Pages/Account/ResetPassword.cshtml.cs`
- Modify: `src/DeviceRental.Web/Pages/Account/Login.cshtml`
- Modify: `src/DeviceRental.Web/Pages/Account/Register.cshtml`

- [x] Render safe generic messages, anti-forgery tokens, validation errors, and links without exposing whether an email exists.
- [x] Add Web tests for page rendering and anti-forgery coverage; full provider-backed Web flow remains in the integration follow-up.

### Task 4: Verification And Delivery

**Files:**
- Modify: `README.md`
- Modify: `docs/test-plan.md`
- Modify: `docs/traceability.csv`
- Modify: `eng/Verify-MigrationArtifact.ps1` only if the model requires a migration.

- [x] Run full unit, Web, PostgreSQL integration, migration, and solution-build checks.
- [x] Commit and push the completed account workflow to `codex/mobile-device-rental-system`.
