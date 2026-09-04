# Outbox Claim And Processor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make notification persistence recoverable by adding PostgreSQL 18 claim/lease CAS transitions and an application processor that classifies sender outcomes without holding database transactions during external I/O.

**Architecture:** `PostgresOutboxStore` owns short `PENDING -> CLAIMED` and lease-guarded `CLAIMED -> SENDING` transitions, plus terminal/retry updates. `OutboxProcessor` invokes an abstract sender only after the SENDING CAS commits, then maps accepted, retryable, permanent, and unknown-acceptance outcomes to persisted states. SMTP templates, recipient decryption, and hosted-worker composition remain a follow-up boundary.

**Tech Stack:** .NET 10, ASP.NET Core hosted services, EF Core/Npgsql, PostgreSQL 18, xUnit v3/Microsoft Testing Platform.

---

### Task 1: Notification Delivery Persistence

- [x] Add `NotificationDeliveryRecord` and constraints for recipient tuples, outcomes, timestamps, and event dedupe.
- [x] Add `NotificationDeliveryAndOperationalIndexes` migration and idempotent deployment SQL.
- [x] Verify migration upgrade/rollback and same-event delivery dedupe on PostgreSQL 18.

### Task 2: Claim And Lease CAS

- [x] Add `IOutboxStore` claim/start APIs.
- [x] Implement `FOR UPDATE SKIP LOCKED` due-row claims and lease-expiry/current-lease CAS checks.
- [x] Verify concurrent claims and expired/current lease behavior on PostgreSQL 18.

### Task 3: Processor Outcome State Machine

- [x] Add sender result contract and `OutboxProcessor` orchestration.
- [x] Keep sender I/O outside database transactions and classify retry/dead-letter/manual-review outcomes.
- [x] Verify accepted, transient, permanent, unknown, and sender-exception paths with unit tests.

### Task 4: Verification And Handoff

- [x] Run unit, Web, database, migration, and solution-build checks for this persistence/processor stage.
- [x] Commit and push the stage; next stage defines encrypted recipient/template payloads and the SMTP sender/hosted worker.
