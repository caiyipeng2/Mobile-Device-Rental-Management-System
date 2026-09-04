# Notification Sender And Worker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decode encrypted notification payloads, render approved account/loan email templates, send through a TLS SMTP adapter, and run the Outbox processor in a hosted Worker.

**Architecture:** AES-GCM payloads carry the recipient and template fields; versioned templates produce plain-text mail. `SmtpNotificationSender` separates malformed payloads (permanent rejection) from transport uncertainty (manual review), while the Worker calls the existing lease/CAS processor on a configurable polling interval.

**Tech Stack:** .NET 10, `System.Net.Mail`, ASP.NET Core options validation, hosted `BackgroundService`, AES-GCM, xUnit v3/Microsoft Testing Platform.

---

### Task 1: Payload And Template Contracts

- [x] Add `NotificationPayload`, rendered mail and template renderer contracts for account verification, password reset, borrow, due, return, force-return, and extension events.
- [x] Add AES-GCM codec with schema/key-version checks and authenticated round-trip tests.

### Task 2: SMTP Sender And Configuration

- [x] Add validated `SmtpOptions` and `SystemNetMailTransport` with TLS.
- [x] Add `SmtpNotificationSender`, mapping malformed payloads to permanent rejection and transport exceptions to unknown acceptance.
- [x] Add sender tests for rendering, transport uncertainty, malformed payloads, and required TLS credentials.

### Task 3: Hosted Worker Composition

- [x] Add validated `WorkerOptions`, PostgreSQL/codec/template/SMTP registrations, and `OutboxWorker` polling loop.
- [x] Continue polling after transient database/transport failures and stop cleanly on cancellation.

### Task 4: Verification And Handoff

- [x] Run solution build, unit, Web, and PostgreSQL integration verification.
- [x] Commit and push this stage; the follow-up business-producer stage is tracked in `2026-09-04-business-notification-producers.md` and now covers encrypted account/loan events plus delivery-history writes.
