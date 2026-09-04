# Business Notification Producers And Delivery History

**Goal:** append encrypted notification events inside account and loan business transactions, then persist one auditable delivery attempt for every send outcome that has a trusted recipient identity.

**Architecture:** `INotificationOutboxWriter` adds an encrypted `PENDING` row to the caller's `DeviceRentalDbContext`; it never calls `SaveChanges`. `OutboxProcessor` records delivery metadata after the sender returns and before the final Outbox state CAS. `PostgresOutboxStore` uses a stable event dedupe key plus an `:attempt:n` history key.

**Completed tasks:**

- [x] Add the transactional Outbox writer and fail-closed/no-op composition for production and demo modes.
- [x] Emit registration verification, verification/password-reset resend, borrowed, self-returned, forced-returned, and extended-loan events with recipient user IDs and operation timestamps.
- [x] Schedule advance/due reminders from the domain planner and cancel pending or claimed reminders on return and extension before creating the replacement plan.
- [x] Add loan-version, due-time, and returned-state checks to the reminder `CLAIMED -> SENDING` CAS and cover a real PostgreSQL processor-to-delivery scenario.
- [x] Persist accepted, transient, permanent, and unknown SMTP outcomes with sanitized evidence and per-attempt dedupe.
- [x] Cover encryption persistence, loan rollback on writer failure, lifecycle event payloads, delivery history, SMTP metadata, and CI test baselines.
- [x] Verify Release build, 129 Unit tests, 39 Web tests, 54 PostgreSQL 18 database tests, migration artifact, and CI skeleton.

**Deferred follow-up:** add host-level `BackgroundService` shutdown/restart and backlog-load scenarios. The current lease, cancellation, and aggregate-state CAS prevents stale reminders from entering `SENDING` after a committed return/extension, while a message already in `SENDING` remains intentionally non-retractable.
