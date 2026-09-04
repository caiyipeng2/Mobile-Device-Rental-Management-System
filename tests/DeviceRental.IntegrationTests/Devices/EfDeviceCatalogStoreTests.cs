using DeviceRental.Application.Devices;
using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Devices;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Devices;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class EfDeviceCatalogStoreTests(PostgresTestEnvironment database)
{
    private static readonly Guid PolicyVersionId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    [Trait("Category", "Database")]
    public async Task ListUnarchivedAsync_ExcludesArchivedDevicesAndIncludesOpenLoanBorrower()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower@example.internal", cancellationToken);
        var device = CreateDevice("DEVICE-001");
        var archived = CreateDevice("DEVICE-ARCHIVED", isArchived: true);
        var now = DateTimeOffset.UtcNow;

        await using (var context = CreateContext())
        {
            context.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));
            context.Devices.Add(DeviceRecordMapper.ToRecord(archived, now, now));
            context.Loans.Add(LoanRecordMapper.ToRecord(Loan.Open(
                Guid.NewGuid(), device.Id, borrower.Id, now, now.AddDays(1), PolicyVersionId)));
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var readContext = CreateContext();
        var store = new EfDeviceCatalogStore(readContext);

        var entries = await store.ListUnarchivedAsync(cancellationToken);

        var entry = Assert.Single(entries);
        Assert.Equal(device.Id, entry.Device.Id);
        Assert.Equal("Borrower", entry.BorrowerName);
        Assert.NotNull(entry.OpenLoan);
        Assert.Equal(borrower.Id, entry.OpenLoan!.BorrowerId);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task TryBorrowAsync_MapsPartialUniqueIndexConflictToAlreadyBorrowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower@example.internal", cancellationToken);
        var device = CreateDevice("DEVICE-002");
        var now = DateTimeOffset.UtcNow;
        await using (var seedContext = CreateContext())
        {
            seedContext.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var firstLoan = Loan.Open(Guid.NewGuid(), device.Id, borrower.Id, now, now.AddDays(1), PolicyVersionId);
        var secondLoan = Loan.Open(Guid.NewGuid(), device.Id, borrower.Id, now, now.AddDays(1), PolicyVersionId);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstStore = new EfDeviceCatalogStore(firstContext);
        var secondStore = new EfDeviceCatalogStore(secondContext);

        var results = await Task.WhenAll(
            firstStore.TryBorrowAsync(firstLoan, cancellationToken),
            secondStore.TryBorrowAsync(secondLoan, cancellationToken));

        Assert.Single(results, result => result.Status == DeviceCatalogStoreWriteStatus.Succeeded);
        Assert.Single(results, result => result.Status == DeviceCatalogStoreWriteStatus.DeviceAlreadyBorrowed);
        await using var verifyContext = CreateContext();
        Assert.Single(await verifyContext.Loans.Where(loan => loan.DeviceId == device.Id && loan.ReturnedAt == null)
            .ToListAsync(cancellationToken));
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task TryBorrowAsync_enqueues_a_borrowed_event_with_the_borrower_identity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower-event@example.internal", cancellationToken);
        var device = CreateDevice("DEVICE-EVENT-001");
        var now = DateTimeOffset.UtcNow;
        await using (var seed = CreateContext())
        {
            seed.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));
            await seed.SaveChangesAsync(cancellationToken);
        }

        var writer = new CapturingNotificationOutboxWriter();
        var loan = Loan.Open(Guid.NewGuid(), device.Id, borrower.Id, now, now.AddDays(1), PolicyVersionId);
        await using var context = CreateContext();
        var result = await new EfDeviceCatalogStore(context, writer).TryBorrowAsync(loan, cancellationToken);

        Assert.Equal(DeviceCatalogStoreWriteStatus.Succeeded, result.Status);
        var notification = Assert.Single(writer.Requests);
        Assert.Equal("LOAN_BORROWED", notification.EventType);
        Assert.Equal("LOAN", notification.AggregateType);
        Assert.Equal(loan.Id.ToString("D"), notification.AggregateId);
        Assert.Equal(borrower.Id, notification.Payload.RecipientUserId);
        Assert.Equal("DEVICE-EVENT-001", notification.Payload.Values["assetNumber"]);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task TryBorrowAsync_rolls_back_the_loan_when_outbox_enqueue_fails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower-rollback@example.internal", cancellationToken);
        var device = CreateDevice("DEVICE-EVENT-002");
        var now = DateTimeOffset.UtcNow;
        await using (var seed = CreateContext())
        {
            seed.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = CreateContext();
        var loan = Loan.Open(Guid.NewGuid(), device.Id, borrower.Id, now, now.AddDays(1), PolicyVersionId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfDeviceCatalogStore(context, new ThrowingNotificationOutboxWriter())
                .TryBorrowAsync(loan, cancellationToken));

        await using var verify = CreateContext();
        Assert.Empty(await verify.Loans.Where(value => value.DeviceId == device.Id).ToListAsync(cancellationToken));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ReturnSelfAsync_OnlyClosesTheBorrowersOpenLoan()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower@example.internal", cancellationToken);
        var anotherUser = await CreateUserAsync("other@example.internal", cancellationToken);
        var device = await CreateBorrowedDeviceAsync(borrower.Id, "DEVICE-003", cancellationToken);
        var returnedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var writer = new CapturingNotificationOutboxWriter();
        await using var context = CreateContext();
        var store = new EfDeviceCatalogStore(context, writer);

        var denied = await store.ReturnSelfAsync(device.Id, anotherUser.Id, returnedAt, cancellationToken);
        var returned = await store.ReturnSelfAsync(device.Id, borrower.Id, returnedAt, cancellationToken);

        Assert.Equal(DeviceCatalogStoreWriteStatus.ReturnNotAuthorized, denied.Status);
        Assert.Equal(DeviceCatalogStoreWriteStatus.Succeeded, returned.Status);
        Assert.Equal(ReturnKind.Self, returned.Loan!.ReturnKind);
        var notification = Assert.Single(writer.Requests);
        Assert.Equal("LOAN_RETURNED", notification.EventType);
        Assert.Equal(borrower.Id, notification.Payload.RecipientUserId);
        await using var verifyContext = CreateContext();
        var persisted = Assert.Single(await verifyContext.Loans.Where(loan => loan.DeviceId == device.Id)
            .ToListAsync(cancellationToken));
        Assert.Equal("SELF", persisted.ReturnKind);
        Assert.Equal(borrower.Id, persisted.ReturnedByUserId);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ForceReturnAndDisableAsync_ClosesLoanAndSuspendsDeviceInOneWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower@example.internal", cancellationToken);
        var administrator = await CreateUserAsync("admin@example.internal", cancellationToken);
        var device = await CreateBorrowedDeviceAsync(borrower.Id, "DEVICE-004", cancellationToken);
        var writer = new CapturingNotificationOutboxWriter();
        await using var context = CreateContext();
        var store = new EfDeviceCatalogStore(context, writer);

        var result = await store.ForceReturnAndDisableAsync(
            device.Id,
            administrator.Id,
            DateTimeOffset.UtcNow.AddMinutes(5),
            Reason.From("屏幕破损待维修"),
            cancellationToken);

        Assert.Equal(DeviceCatalogStoreWriteStatus.Succeeded, result.Status);
        Assert.Equal(ReturnKind.Forced, result.Loan!.ReturnKind);
        var notification = Assert.Single(writer.Requests);
        Assert.Equal("LOAN_FORCED_RETURN", notification.EventType);
        Assert.Equal(borrower.Id, notification.Payload.RecipientUserId);
        await using var verifyContext = CreateContext();
        var persistedDevice = await verifyContext.Devices.SingleAsync(value => value.Id == device.Id, cancellationToken);
        Assert.Equal("TEMPORARILY_DISABLED", persistedDevice.ManualState);
        Assert.Equal("屏幕破损待维修", persistedDevice.TemporaryUnavailableReason);
        var persistedLoan = await verifyContext.Loans.SingleAsync(value => value.DeviceId == device.Id, cancellationToken);
        Assert.NotNull(persistedLoan.ReturnedAt);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ExtendAsync_PersistsAdministratorExtensionUsingTheDomainPolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var borrower = await CreateUserAsync("borrower@example.internal", cancellationToken);
        var administrator = await CreateUserAsync("admin@example.internal", cancellationToken);
        var device = await CreateBorrowedDeviceAsync(borrower.Id, "DEVICE-005", cancellationToken);
        var effectiveNow = DateTimeOffset.UtcNow.AddMinutes(5);
        var writer = new CapturingNotificationOutboxWriter();
        await using var context = CreateContext();
        var store = new EfDeviceCatalogStore(context, writer);

        var result = await store.ExtendAsync(
            device.Id,
            administrator.Id,
            DurationMinutes.From(120),
            Reason.From("测试计划延长"),
            effectiveNow,
            cancellationToken);

        Assert.Equal(DeviceCatalogStoreWriteStatus.Succeeded, result.Status);
        Assert.NotNull(result.Extension);
        var notification = Assert.Single(writer.Requests);
        Assert.Equal("LOAN_EXTENDED", notification.EventType);
        Assert.Equal(borrower.Id, notification.Payload.RecipientUserId);
        await using var verifyContext = CreateContext();
        var persisted = await verifyContext.Loans.SingleAsync(value => value.DeviceId == device.Id, cancellationToken);
        Assert.Equal(result.Loan!.DueAtUtc, persisted.DueAt);
        Assert.Equal(result.Extension!.NewDueAtUtc, persisted.DueAt);
    }

    private async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private async Task<ApplicationUser> CreateUserAsync(string email, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            RealName = email.StartsWith("borrower", StringComparison.Ordinal) ? "Borrower" : "Test User",
            IsActive = true,
            AuthorizationVersion = 1,
            LockoutEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await using var context = CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<Device> CreateBorrowedDeviceAsync(
        Guid borrowerId,
        string assetNumber,
        CancellationToken cancellationToken)
    {
        var device = CreateDevice(assetNumber);
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));
        context.Loans.Add(LoanRecordMapper.ToRecord(Loan.Open(
            Guid.NewGuid(), device.Id, borrowerId, now, now.AddDays(1), PolicyVersionId)));
        await context.SaveChangesAsync(cancellationToken);
        return device;
    }

    private static Device CreateDevice(string assetNumber, bool isArchived = false) =>
        new(Guid.NewGuid(), assetNumber, "Pixel 9", DeviceTier.High, Guid.NewGuid(), isArchived: isArchived);

    private sealed class CapturingNotificationOutboxWriter : INotificationOutboxWriter
    {
        public List<NotificationOutboxRequest> Requests { get; } = [];

        public void Enqueue(NotificationOutboxRequest request) => Requests.Add(request);
    }

    private sealed class ThrowingNotificationOutboxWriter : INotificationOutboxWriter
    {
        public void Enqueue(NotificationOutboxRequest request) =>
            throw new InvalidOperationException("synthetic outbox failure");
    }
}
