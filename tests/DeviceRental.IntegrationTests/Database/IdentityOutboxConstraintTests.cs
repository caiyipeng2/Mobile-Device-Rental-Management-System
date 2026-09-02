using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace DeviceRental.IntegrationTests.Database;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class IdentityOutboxConstraintTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUTH-002")]
    public async Task NormalizedEmail_IsRequiredAndUnique()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(NormalizedEmail_IsRequiredAndUnique), cancellationToken);

        var missingEmail = await CapturePostgresExceptionAsync(() =>
            InsertUserAsync(connection, Guid.NewGuid(), "missing@example.test", null, cancellationToken));
        AssertSqlState(PostgresErrorCodes.NotNullViolation, missingEmail.SqlState);

        var mismatchedVerification = await CapturePostgresExceptionAsync(() =>
            InsertUserAsync(
                connection,
                Guid.NewGuid(),
                "mismatch@example.test",
                "MISMATCH@EXAMPLE.TEST",
                cancellationToken,
                emailConfirmed: true));
        AssertSqlState(PostgresErrorCodes.CheckViolation, mismatchedVerification.SqlState);

        var timestampWithoutConfirmation = await CapturePostgresExceptionAsync(() =>
            InsertUserAsync(
                connection,
                Guid.NewGuid(),
                "timestamp-only@example.test",
                "TIMESTAMP-ONLY@EXAMPLE.TEST",
                cancellationToken,
                emailVerifiedAt: DateTimeOffset.UtcNow));
        AssertSqlState(PostgresErrorCodes.CheckViolation, timestampWithoutConfirmation.SqlState);

        await InsertUserAsync(
            connection,
            Guid.NewGuid(),
            "verified@example.test",
            "VERIFIED@EXAMPLE.TEST",
            cancellationToken,
            emailConfirmed: true,
            emailVerifiedAt: DateTimeOffset.UtcNow);

        await InsertUserAsync(
            connection,
            Guid.NewGuid(),
            "first@example.test",
            "DUPLICATE@EXAMPLE.TEST",
            cancellationToken);
        var duplicateEmail = await CapturePostgresExceptionAsync(() =>
            InsertUserAsync(
                connection,
                Guid.NewGuid(),
                "second@example.test",
                "DUPLICATE@EXAMPLE.TEST",
                cancellationToken));
        AssertSqlState(PostgresErrorCodes.UniqueViolation, duplicateEmail.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUTH-004")]
    public async Task Roles_AreRestrictedToApprovedWhitelist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(Roles_AreRestrictedToApprovedWhitelist), cancellationToken);

        await InsertRoleAsync(connection, "USER", cancellationToken);
        await InsertRoleAsync(connection, "TEST_ADMIN", cancellationToken);
        var invalidRole = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertRoleAsync(connection, "SYSTEM_ADMIN", cancellationToken));
        AssertSqlState(PostgresErrorCodes.CheckViolation, invalidRole.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-001")]
    public async Task AuditFields_AreRequiredAndStoredAsJsonb()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(AuditFields_AreRequiredAndStoredAsJsonb), cancellationToken);

        const string columnSql = """
            SELECT column_name, udt_name, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'device_rental'
              AND table_name = 'audit_events'
              AND column_name IN ('actor_kind', 'event_type', 'subject_type',
                                  'subject_id', 'changed_fields_json', 'correlation_id', 'created_at')
            ORDER BY column_name;
            """;
        await using (var command = new NpgsqlCommand(columnSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var columns = new Dictionary<string, (string Type, string Nullable)>(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2)));
            }

            Assert.Equal(7, columns.Count);
            Assert.All(columns.Values, column => Assert.Equal("NO", column.Nullable));
            Assert.Equal("jsonb", columns["changed_fields_json"].Type);
        }

        var missingEventType = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(connection, eventType: null, cancellationToken));
        AssertSqlState(PostgresErrorCodes.NotNullViolation, missingEventType.SqlState);

        var missingCorrelation = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                eventType: "DEVICE_CREATED",
                cancellationToken,
                nullCorrelationId: true));
        AssertSqlState(PostgresErrorCodes.NotNullViolation, missingCorrelation.SqlState);

        foreach (var invalidShape in new[] { "[]", "{}", "{\"before\":{}}", "{\"after\":{}}" })
        {
            var invalidChangedFields = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertAuditAsync(
                    connection,
                    eventType: "DEVICE_CREATED",
                    cancellationToken,
                    changedFieldsJson: invalidShape));
            AssertSqlState(PostgresErrorCodes.CheckViolation, invalidChangedFields.SqlState);
        }

        const string malformedJsonSql = "SELECT CAST(@document AS jsonb);";
        await using var malformedJson = new NpgsqlCommand(malformedJsonSql, connection);
        malformedJson.Parameters.AddWithValue("document", "{not-json");
        var invalidJson = await Assert.ThrowsAsync<PostgresException>(() =>
            malformedJson.ExecuteNonQueryAsync(cancellationToken));
        AssertSqlState(PostgresErrorCodes.InvalidTextRepresentation, invalidJson.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-001")]
    public async Task InvalidAuditActorTuples_AreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(InvalidAuditActorTuples_AreRejected), cancellationToken);
        var userId = Guid.NewGuid();
        await InsertUserAsync(
            connection,
            userId,
            "audit-actor@example.test",
            "AUDIT-ACTOR@EXAMPLE.TEST",
            cancellationToken);

        var systemWithUser = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "SYSTEM",
                actorUserId: userId));
        AssertSqlState(PostgresErrorCodes.CheckViolation, systemWithUser.SqlState);

        var userWithoutId = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "USER"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, userWithoutId.SqlState);

        var userWithExternalIdentifier = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "USER",
                actorUserId: userId,
                externalActorIdentifier: "deployment-42"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, userWithExternalIdentifier.SqlState);

        var userWithoutIdWithExternalIdentifier = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "USER",
                externalActorIdentifier: "deployment-42"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, userWithoutIdWithExternalIdentifier.SqlState);

        var systemWithExternalIdentifier = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "SYSTEM",
                externalActorIdentifier: "deployment-42"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, systemWithExternalIdentifier.SqlState);

        var systemWithBothIdentifiers = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "SYSTEM",
                actorUserId: userId,
                externalActorIdentifier: "deployment-42"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, systemWithBothIdentifiers.SqlState);

        var operationsWithoutIdentifier = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "OPERATIONS"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, operationsWithoutIdentifier.SqlState);

        var operationsWithUser = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "OPERATIONS",
                actorUserId: userId,
                externalActorIdentifier: "deployment-42"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, operationsWithUser.SqlState);

        var operationsWithUserOnly = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "OPERATIONS",
                actorUserId: userId));
        AssertSqlState(PostgresErrorCodes.CheckViolation, operationsWithUserOnly.SqlState);

        var unknownActorKind = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAuditAsync(
                connection,
                "DEVICE_CREATED",
                cancellationToken,
                actorKind: "SERVICE"));
        AssertSqlState(PostgresErrorCodes.CheckViolation, unknownActorKind.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-003")]
    public async Task AuditRows_CannotBeUpdatedOrDeletedByApplicationRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(AuditRows_CannotBeUpdatedOrDeletedByApplicationRole), cancellationToken);
        var eventId = await InsertAuditAsync(connection, "DEVICE_CREATED", cancellationToken);

        var update = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "UPDATE device_rental.audit_events SET event_type = 'TAMPERED' WHERE event_id = @id;",
            cancellationToken,
            new NpgsqlParameter("id", eventId)));
        AssertSqlState(PostgresErrorCodes.InsufficientPrivilege, update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "DELETE FROM device_rental.audit_events WHERE event_id = @id;",
            cancellationToken,
            new NpgsqlParameter("id", eventId)));
        AssertSqlState(PostgresErrorCodes.InsufficientPrivilege, delete.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task OutboxDedupeKey_IsRequiredAndUnique()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(OutboxDedupeKey_IsRequiredAndUnique), cancellationToken);

        var missingKey = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertPendingOutboxAsync(connection, null, cancellationToken));
        AssertSqlState(PostgresErrorCodes.NotNullViolation, missingKey.SqlState);

        var missingCorrelation = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertOutboxAsync(
                connection,
                "loan:missing:correlation",
                "PENDING",
                payloadSchemaVersion: 1,
                payloadKeyVersion: "key-v1",
                payloadCiphertext: [1, 2, 3],
                cancellationToken: cancellationToken,
                nullCorrelationId: true));
        AssertSqlState(PostgresErrorCodes.NotNullViolation, missingCorrelation.SqlState);

        await InsertPendingOutboxAsync(connection, "loan:42:borrowed", cancellationToken);
        var duplicateKey = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertPendingOutboxAsync(connection, "loan:42:borrowed", cancellationToken));
        AssertSqlState(PostgresErrorCodes.UniqueViolation, duplicateKey.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task InvalidOutboxStatus_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(InvalidOutboxStatus_IsRejected), cancellationToken);

        var invalidStatus = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertOutboxAsync(
                connection,
                "loan:status:invalid",
                "NOT_A_STATUS",
                payloadSchemaVersion: 1,
                payloadKeyVersion: "key-v1",
                payloadCiphertext: [1, 2, 3],
                cancellationToken: cancellationToken));
        AssertSqlState(PostgresErrorCodes.CheckViolation, invalidStatus.SqlState);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public async Task InvalidLeaseTuple_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(InvalidLeaseTuple_IsRejected), cancellationToken);

        var now = DateTimeOffset.UtcNow;
        (Guid? LeaseId, string? LockedBy, DateTimeOffset? LockedUntil)[] invalidLeases =
        [
            (Guid.NewGuid(), null, null),
            (null, "worker-1", null),
            (null, null, now.AddMinutes(10)),
            (Guid.NewGuid(), "worker-1", null),
            (Guid.NewGuid(), null, now.AddMinutes(10)),
            (null, "worker-1", now.AddMinutes(10)),
        ];

        for (var index = 0; index < invalidLeases.Length; index++)
        {
            var lease = invalidLeases[index];
            var invalidLease = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertOutboxAsync(
                    connection,
                    $"loan:lease:partial:{index}",
                    "CLAIMED",
                    payloadSchemaVersion: 1,
                    payloadKeyVersion: "key-v1",
                    payloadCiphertext: [1, 2, 3],
                    cancellationToken: cancellationToken,
                    leaseId: lease.LeaseId,
                    lockedBy: lease.LockedBy,
                    lockedUntil: lease.LockedUntil,
                    createdAt: now));
            AssertSqlState(PostgresErrorCodes.CheckViolation, invalidLease.SqlState);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task InvalidPayloadTuple_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(InvalidPayloadTuple_IsRejected), cancellationToken);

        (int? SchemaVersion, string? KeyVersion, byte[]? Ciphertext)[] invalidPayloads =
        [
            (1, null, null),
            (null, "key-v1", null),
            (null, null, [1, 2, 3]),
            (1, "key-v1", null),
            (1, null, [1, 2, 3]),
            (null, "key-v1", [1, 2, 3]),
        ];

        for (var index = 0; index < invalidPayloads.Length; index++)
        {
            var payload = invalidPayloads[index];
            var invalidPayload = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertOutboxAsync(
                    connection,
                    $"loan:payload:partial:{index}",
                    "PENDING",
                    payloadSchemaVersion: payload.SchemaVersion,
                    payloadKeyVersion: payload.KeyVersion,
                    payloadCiphertext: payload.Ciphertext,
                    cancellationToken: cancellationToken));
            AssertSqlState(PostgresErrorCodes.CheckViolation, invalidPayload.SqlState);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ProcessedState_RequiresCompleteTerminalAndPurgedPayloadTuples()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(ProcessedState_RequiresCompleteTerminalAndPurgedPayloadTuples), cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var missingStateTimes = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertOutboxAsync(
                connection,
                "loan:processed:missing-times",
                "PROCESSED",
                payloadSchemaVersion: 1,
                payloadKeyVersion: "key-v1",
                payloadCiphertext: [1, 2, 3],
                cancellationToken: cancellationToken,
                attempts: 1,
                createdAt: now));
        AssertSqlState(PostgresErrorCodes.CheckViolation, missingStateTimes.SqlState);

        var missingPurgeTime = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertOutboxAsync(
                connection,
                "loan:processed:missing-purge",
                "PROCESSED",
                payloadSchemaVersion: null,
                payloadKeyVersion: null,
                payloadCiphertext: null,
                cancellationToken: cancellationToken,
                attempts: 1,
                leaseId: Guid.NewGuid(),
                lockedBy: "worker-1",
                lockedUntil: now.AddMinutes(10),
                sendingStartedAt: now.AddMinutes(1),
                processedAt: now.AddMinutes(2),
                createdAt: now));
        AssertSqlState(PostgresErrorCodes.CheckViolation, missingPurgeTime.SqlState);

        var retainedAndPurged = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertOutboxAsync(
                connection,
                "loan:processed:payload-and-purge",
                "PROCESSED",
                payloadSchemaVersion: 1,
                payloadKeyVersion: "key-v1",
                payloadCiphertext: [1, 2, 3],
                cancellationToken: cancellationToken,
                attempts: 1,
                leaseId: Guid.NewGuid(),
                lockedBy: "worker-1",
                lockedUntil: now.AddMinutes(10),
                sendingStartedAt: now.AddMinutes(1),
                processedAt: now.AddMinutes(2),
                payloadPurgedAt: now.AddMinutes(3),
                createdAt: now));
        AssertSqlState(PostgresErrorCodes.CheckViolation, retainedAndPurged.SqlState);

        await InsertOutboxAsync(
            connection,
            "loan:processed:valid-purged",
            "PROCESSED",
            payloadSchemaVersion: null,
            payloadKeyVersion: null,
            payloadCiphertext: null,
            cancellationToken: cancellationToken,
            attempts: 1,
            leaseId: Guid.NewGuid(),
            lockedBy: "worker-1",
            lockedUntil: now.AddMinutes(10),
            sendingStartedAt: now.AddMinutes(1),
            processedAt: now.AddMinutes(2),
            payloadPurgedAt: now.AddMinutes(3),
            createdAt: now);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ApprovedOutboxStateTruthTable_AcceptsEveryState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(ApprovedOutboxStateTruthTable_AcceptsEveryState), cancellationToken);
        var now = DateTimeOffset.UtcNow;

        await InsertPendingOutboxAsync(connection, "truth:pending", cancellationToken);
        await InsertOutboxAsync(
            connection, "truth:pending-retry", "PENDING", 1, "key-v1", [1], cancellationToken,
            attempts: 1, lastError: "temporary refusal", createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:claimed", "CLAIMED", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:claimed-retry", "CLAIMED", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            attempts: 1, lastError: "temporary refusal", createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:sending", "SENDING", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            attempts: 1, sendingStartedAt: now.AddMinutes(1), createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:processed-payload", "PROCESSED", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            attempts: 1, sendingStartedAt: now.AddMinutes(1), processedAt: now.AddMinutes(2),
            createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:dead-letter", "DEAD_LETTER", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            attempts: 1, sendingStartedAt: now.AddMinutes(1), failedAt: now.AddMinutes(2),
            lastError: "permanent refusal", createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:review", "REVIEW_REQUIRED", 1, "key-v1", [1], cancellationToken,
            leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
            attempts: 1, sendingStartedAt: now.AddMinutes(1), failedAt: now.AddMinutes(2),
            lastError: "acceptance unknown", createdAt: now);
        await InsertOutboxAsync(
            connection, "truth:cancelled", "CANCELLED", 1, "key-v1", [1], cancellationToken,
            canceledAt: now.AddMinutes(1), createdAt: now);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task OutboxTimeAttemptAndErrorInvariants_AreEnforced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var connection = await OpenApplicationConnectionAsync(
            nameof(OutboxTimeAttemptAndErrorInvariants_AreEnforced), cancellationToken);
        var now = DateTimeOffset.UtcNow;

        Func<Task>[] invalidRows =
        [
            () => InsertOutboxAsync(
                connection, "invalid:available", "PENDING", 1, "key-v1", [1], cancellationToken,
                createdAt: now, availableAt: now.AddSeconds(-1)),
            () => InsertOutboxAsync(
                connection, "invalid:attempt-negative", "PENDING", 1, "key-v1", [1], cancellationToken,
                attempts: -1, createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:pending-attempt", "PENDING", 1, "key-v1", [1], cancellationToken,
                attempts: 1, createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:pending-error", "PENDING", 1, "key-v1", [1], cancellationToken,
                lastError: "unexpected", createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:lock-time", "CLAIMED", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now,
                createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:sending-attempt", "SENDING", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                sendingStartedAt: now.AddMinutes(1), createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:sending-before-available", "SENDING", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, availableAt: now.AddMinutes(2), sendingStartedAt: now.AddMinutes(1),
                createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:sending-after-lease", "SENDING", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(1),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:sending-error", "SENDING", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), lastError: "unexpected", createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:processed-before-send", "PROCESSED", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(2), processedAt: now.AddMinutes(1),
                createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:terminal-double", "PROCESSED", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), processedAt: now.AddMinutes(2),
                failedAt: now.AddMinutes(2), createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:failure-no-error", "DEAD_LETTER", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), failedAt: now.AddMinutes(2),
                createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:failure-no-attempt", "DEAD_LETTER", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                sendingStartedAt: now.AddMinutes(1), failedAt: now.AddMinutes(2),
                lastError: "permanent refusal", createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:processed-error", "PROCESSED", 1, "key-v1", [1], cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), processedAt: now.AddMinutes(2),
                lastError: "unexpected", createdAt: now),
            () => InsertOutboxAsync(
                connection, "invalid:purge-before-terminal", "PROCESSED", null, null, null, cancellationToken,
                leaseId: Guid.NewGuid(), lockedBy: "worker-1", lockedUntil: now.AddMinutes(10),
                attempts: 1, sendingStartedAt: now.AddMinutes(1), processedAt: now.AddMinutes(3),
                payloadPurgedAt: now.AddMinutes(2), createdAt: now),
        ];

        foreach (var invalidRow in invalidRows)
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(invalidRow);
            AssertSqlState(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ConcurrentOutboxInserts_AllowOnlyOneDedupeKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        await using var first = await OpenApplicationConnectionAsync(
            nameof(ConcurrentOutboxInserts_AllowOnlyOneDedupeKey) + ":first", cancellationToken);
        await using var second = await OpenApplicationConnectionAsync(
            nameof(ConcurrentOutboxInserts_AllowOnlyOneDedupeKey) + ":second", cancellationToken);
        await using var firstTransaction = await first.BeginTransactionAsync(cancellationToken);

        await InsertPendingOutboxAsync(first, "loan:concurrent:borrowed", cancellationToken);
        var competingInsert = InsertPendingOutboxAsync(
            second,
            "loan:concurrent:borrowed",
            cancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        Assert.False(
            competingInsert.IsCompleted,
            "The competing insert must wait on the unique index until the first transaction resolves.");
        await firstTransaction.CommitAsync(cancellationToken);

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => competingInsert);
        AssertSqlState(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    private async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DatabaseReset.ResetAsync(database, cancellationToken);
            await using var context = InfrastructureDbContextFactory.Create(
                database.MigrationConnectionString);
            Assert.NotEmpty(context.Database.GetMigrations());
            await context.Database.MigrateAsync(cancellationToken);
            await DatabaseReset.GrantApplicationAccessAsync(database, cancellationToken);
        }
        catch (Exception exception)
        {
            var sqlState = exception is PostgresException postgres
                ? postgres.SqlState
                : "none";
            var diagnosticDirectory = Environment.GetEnvironmentVariable(
                "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY") ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(diagnosticDirectory);
            await File.AppendAllTextAsync(
                Path.Combine(diagnosticDirectory, "safe-diagnostics.txt"),
                $"PrepareDatabaseAsync|{exception.GetType().Name}|{sqlState}{Environment.NewLine}",
                cancellationToken);
            WriteSafeDiagnostic($"SAFE-PREPARE|{exception.GetType().Name}|{sqlState}");
            throw;
        }
    }

    private static async Task InsertUserAsync(
        NpgsqlConnection connection,
        Guid id,
        string email,
        string? normalizedEmail,
        CancellationToken cancellationToken,
        bool emailConfirmed = false,
        DateTimeOffset? emailVerifiedAt = null)
    {
        var createdAt = emailVerifiedAt ?? DateTimeOffset.UtcNow;
        const string sql = """
            INSERT INTO device_rental.users
                (id, user_name, normalized_user_name, email, normalized_email,
                 email_confirmed, phone_number_confirmed, two_factor_enabled,
                 lockout_enabled, access_failed_count, real_name, is_active,
                 authorization_version, email_verified_at, created_at, updated_at)
            VALUES
                (@id, @email, @normalized_user_name, @email, @normalized_email,
                 @email_confirmed, FALSE, FALSE, TRUE, 0, 'Test User', TRUE, 1,
                 @email_verified_at, @created_at, @updated_at);
            """;
        await ExecuteAsync(
            connection,
            sql,
            cancellationToken,
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("email", email),
            new NpgsqlParameter("normalized_user_name", email.ToUpperInvariant()),
            new NpgsqlParameter("normalized_email", (object?)normalizedEmail ?? DBNull.Value),
            new NpgsqlParameter("email_confirmed", emailConfirmed),
            new NpgsqlParameter("email_verified_at", (object?)emailVerifiedAt ?? DBNull.Value),
            new NpgsqlParameter("created_at", createdAt),
            new NpgsqlParameter("updated_at", createdAt));
    }

    private static Task InsertRoleAsync(
        NpgsqlConnection connection,
        string role,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            "INSERT INTO device_rental.roles (id, name, normalized_name) " +
            "VALUES (@id, @name, @normalized_name);",
            cancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("name", role),
            new NpgsqlParameter("normalized_name", role));

    private static async Task<Guid> InsertAuditAsync(
        NpgsqlConnection connection,
        string? eventType,
        CancellationToken cancellationToken,
        string changedFieldsJson = "{\"before\":{},\"after\":{\"state\":\"AVAILABLE\"}}",
        string actorKind = "SYSTEM",
        Guid? actorUserId = null,
        string? externalActorIdentifier = null,
        bool nullCorrelationId = false)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO device_rental.audit_events
                (event_id, actor_kind, actor_user_id, external_actor_identifier,
                 event_type, subject_type, subject_id,
                 changed_fields_json, correlation_id, created_at)
            VALUES
                (@id, @actor_kind, @actor_user_id, @external_actor_identifier,
                 @event_type, 'DEVICE', @subject_id,
                 @changed_fields_json, @correlation_id, @created_at);
            """;
        await ExecuteAsync(
            connection,
            sql,
            cancellationToken,
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("actor_kind", actorKind),
            new NpgsqlParameter("actor_user_id", (object?)actorUserId ?? DBNull.Value),
            new NpgsqlParameter("external_actor_identifier", (object?)externalActorIdentifier ?? DBNull.Value),
            new NpgsqlParameter("event_type", (object?)eventType ?? DBNull.Value),
            new NpgsqlParameter("subject_id", Guid.NewGuid().ToString("N")),
            new NpgsqlParameter("changed_fields_json", NpgsqlDbType.Jsonb)
            {
                Value = changedFieldsJson,
            },
            new NpgsqlParameter(
                "correlation_id",
                nullCorrelationId ? DBNull.Value : Guid.NewGuid().ToString("N")),
            new NpgsqlParameter("created_at", DateTimeOffset.UtcNow));
        return id;
    }

    private static Task InsertPendingOutboxAsync(
        NpgsqlConnection connection,
        string? deduplicationKey,
        CancellationToken cancellationToken) =>
        InsertOutboxAsync(
            connection,
            deduplicationKey,
            "PENDING",
            payloadSchemaVersion: 1,
            payloadKeyVersion: "key-v1",
            payloadCiphertext: [1, 2, 3],
            cancellationToken: cancellationToken);

    private static Task InsertOutboxAsync(
        NpgsqlConnection connection,
        string? deduplicationKey,
        string status,
        int? payloadSchemaVersion,
        string? payloadKeyVersion,
        byte[]? payloadCiphertext,
        CancellationToken cancellationToken,
        Guid? leaseId = null,
        string? lockedBy = null,
        DateTimeOffset? lockedUntil = null,
        int attempts = 0,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? availableAt = null,
        DateTimeOffset? sendingStartedAt = null,
        DateTimeOffset? processedAt = null,
        DateTimeOffset? canceledAt = null,
        DateTimeOffset? failedAt = null,
        string? lastError = null,
        DateTimeOffset? payloadPurgedAt = null,
        bool nullCorrelationId = false)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        const string sql = """
            INSERT INTO device_rental.outbox_messages
                (event_id, dedupe_key, event_type, aggregate_type, aggregate_id,
                 aggregate_version, correlation_id, payload_schema_version,
                 payload_key_version, payload_ciphertext, created_at,
                 available_at, status, attempts, lease_id, locked_by,
                 locked_until, sending_started_at, processed_at, canceled_at,
                 failed_at, last_error, payload_purged_at)
            VALUES
                (@id, @dedupe_key, 'LOAN_BORROWED', 'LOAN', @aggregate_id,
                 1, @correlation_id, @payload_schema_version,
                 @payload_key_version, @payload_ciphertext, @created_at,
                 @available_at, @status, @attempts, @lease_id, @locked_by,
                 @locked_until, @sending_started_at, @processed_at, @canceled_at,
                 @failed_at, @last_error, @payload_purged_at);
            """;
        return ExecuteAsync(
            connection,
            sql,
            cancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("dedupe_key", (object?)deduplicationKey ?? DBNull.Value),
            new NpgsqlParameter("aggregate_id", Guid.NewGuid().ToString("N")),
            new NpgsqlParameter(
                "correlation_id",
                nullCorrelationId ? DBNull.Value : Guid.NewGuid().ToString("N")),
            new NpgsqlParameter("payload_schema_version", (object?)payloadSchemaVersion ?? DBNull.Value),
            new NpgsqlParameter("payload_key_version", (object?)payloadKeyVersion ?? DBNull.Value),
            new NpgsqlParameter("payload_ciphertext", (object?)payloadCiphertext ?? DBNull.Value),
            new NpgsqlParameter("created_at", now),
            new NpgsqlParameter("available_at", availableAt ?? now),
            new NpgsqlParameter("status", status),
            new NpgsqlParameter("attempts", attempts),
            new NpgsqlParameter("lease_id", (object?)leaseId ?? DBNull.Value),
            new NpgsqlParameter("locked_by", (object?)lockedBy ?? DBNull.Value),
            new NpgsqlParameter("locked_until", (object?)lockedUntil ?? DBNull.Value),
            new NpgsqlParameter("sending_started_at", (object?)sendingStartedAt ?? DBNull.Value),
            new NpgsqlParameter("processed_at", (object?)processedAt ?? DBNull.Value),
            new NpgsqlParameter("canceled_at", (object?)canceledAt ?? DBNull.Value),
            new NpgsqlParameter("failed_at", (object?)failedAt ?? DBNull.Value),
            new NpgsqlParameter("last_error", (object?)lastError ?? DBNull.Value),
            new NpgsqlParameter("payload_purged_at", (object?)payloadPurgedAt ?? DBNull.Value));
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            var sqlState = exception is PostgresException postgres
                ? postgres.SqlState
                : "none";
            WriteSafeDiagnostic($"SAFE-SQL|{exception.GetType().Name}|{sqlState}");
            throw;
        }
    }

    private static void AssertSqlState(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var directory = Environment.GetEnvironmentVariable(
                "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY") ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "sqlstate-assertions.txt"),
                $"expected={expected}|actual={actual}{Environment.NewLine}");
        }

        Assert.Equal(expected, actual);
    }

    private async Task<NpgsqlConnection> OpenApplicationConnectionAsync(
        string operation,
        CancellationToken cancellationToken)
    {
        var connection = database.CreateApplicationConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            var sqlState = exception is PostgresException postgres
                ? postgres.SqlState
                : "none";
            var directory = Environment.GetEnvironmentVariable(
                "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY") ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);
            await File.AppendAllTextAsync(
                Path.Combine(directory, "connection-diagnostics.txt"),
                $"{operation}|{exception.GetType().Name}|{sqlState}{Environment.NewLine}",
                cancellationToken);
            WriteSafeDiagnostic($"SAFE-DIAGNOSTIC|{operation}|{exception.GetType().Name}|{sqlState}");
            throw;
        }
    }

    private static void WriteSafeDiagnostic(string line)
    {
        Console.Error.WriteLine(line);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(AppContext.BaseDirectory, "safe-diagnostics.txt"),
        };
        var configuredDirectory = Environment.GetEnvironmentVariable(
            "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            paths.Add(Path.Combine(configuredDirectory, "safe-diagnostics.txt"));
        }

        foreach (var path in paths)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Diagnostics must never mask the underlying test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Diagnostics must never mask the underlying test result.
            }
        }
    }

    private static async Task<PostgresException> CapturePostgresExceptionAsync(
        Func<Task> action,
        [System.Runtime.CompilerServices.CallerMemberName] string? operation = null)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            var sqlState = exception is NpgsqlException npgsql && npgsql.InnerException is PostgresException postgres
                ? postgres.SqlState
                : "none";
            var directory = Environment.GetEnvironmentVariable(
                "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY") ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);
            await File.AppendAllTextAsync(
                Path.Combine(directory, "exception-diagnostics.txt"),
                $"{operation}|{exception.GetType().Name}|{sqlState}{Environment.NewLine}");
            WriteSafeDiagnostic($"SAFE-DIAGNOSTIC|{operation}|{exception.GetType().Name}|{sqlState}");
            throw;
        }

        var noExceptionDirectory = Environment.GetEnvironmentVariable(
            "DEVICERENTAL_SAFE_DIAGNOSTICS_DIRECTORY") ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(noExceptionDirectory);
        await File.AppendAllTextAsync(
            Path.Combine(noExceptionDirectory, "exception-diagnostics.txt"),
            $"{operation}|ExpectedExceptionNotRaised|none{Environment.NewLine}");
        WriteSafeDiagnostic($"SAFE-DIAGNOSTIC|{operation}|ExpectedExceptionNotRaised|none");
        throw new InvalidOperationException(
            $"Expected PostgreSQL exception was not raised in {operation}.");
    }
}
