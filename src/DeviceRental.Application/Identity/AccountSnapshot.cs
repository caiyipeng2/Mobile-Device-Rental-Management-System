namespace DeviceRental.Application.Identity;

public enum AccountRole
{
    User,
    TestAdmin,
}

public sealed record AccountSnapshot(
    Guid Id,
    string Email,
    string RealName,
    AccountRole Role,
    bool IsEmailVerified,
    bool IsActive,
    long AuthorizationVersion,
    DateTimeOffset? LockedUntilUtc)
{
    public static AccountSnapshot PendingVerification(
        Guid id,
        string email,
        string realName,
        AccountRole role) =>
        new(id, email, realName, role, false, true, 1, null);
}

public sealed record NewAccount(string Email, string RealName, string Password, AccountRole Role);
