using Microsoft.AspNetCore.Identity;

namespace Halisaha.Infrastructure.Identity;

/// <summary>Login data only. Domain data lives in PlayerProfile (same id).</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
