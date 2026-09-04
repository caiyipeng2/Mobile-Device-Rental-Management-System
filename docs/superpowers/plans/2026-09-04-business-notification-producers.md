# Business Notification Producers And Delivery History

**Goal:** append encrypted notification events inside account and loan business transactions, then persist one auditable delivery attempt for every send outcome that has a trusted recipient identity.

**Architecture:** `INotificationOutboxWriter` adds an encrypted `PENDING` row to the caller's `DeviceRentalDbContext`; it never calls `SaveChanges`. `OutboxProcessor` records delivery metadata after the sender returns and before the final Outbox state CAS. `PostgresOutboxStore` uses a stable event dedupe key plus an `:attempt:n` history key.

**Completed tasks:**

- [x] Add the transactional Outbox writer and fail-closed/no-op composition for production and demo modes.
- [x] Emit registration verification, borrowed, self-returned, forced-returned, and extended-loan events with recipient user IDs and operation timestamps.
- [x] Persist accepted, transient, permanent, and unknown SMTP outcomes with sanitized evidence and per-attempt dedupe.
- [x] Cover encryption persistence, loan rollback on writer failure, lifecycle event payloads, delivery history, SMTP metadata, and CI test baselines.
- [x] Verify Release build, 129 Unit tests, 38 Web tests, 48 PostgreSQL 18 database tests, migration artifact, and CI skeleton.

**Deferred follow-up:** generate advance/due reminder events, cancel pending reminders on return/extension, and enqueue verification/password-reset resend events. These remain explicitly marked as planned in the requirement traceability files.
