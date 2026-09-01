using Microsoft.AspNetCore.Identity;

namespace DeviceRental.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string RealName { get; set; }

    public bool IsActive { get; set; } = true;

    public long AuthorizationVersion { get; set; } = 1;

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
