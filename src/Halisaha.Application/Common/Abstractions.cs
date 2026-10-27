using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Common;

/// <summary>Data access used by the use cases. Implemented by Infrastructure with EF Core + PostgreSQL.</summary>
public interface IApplicationDbContext
{
    DbSet<PlayerProfile> PlayerProfiles { get; }
    DbSet<Province> Provinces { get; }
    DbSet<District> Districts { get; }
    DbSet<Venue> Venues { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchRequest> MatchRequests { get; }
    DbSet<Rating> Ratings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Starts a database transaction. Dispose without commit to roll back.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Locks the match row until the current transaction ends (SELECT ... FOR UPDATE).
    /// Used for acceptances so two of them cannot take the last slot at the same time.
    /// Returns false if the match does not exist.
    /// </summary>
    Task<bool> LockMatchAsync(Guid matchId, CancellationToken ct = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}

/// <summary>Identity (login data) lives in Infrastructure; the domain only knows user ids.</summary>
public interface IIdentityService
{
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
    Task<AuthenticatedUser?> CheckCredentialsAsync(string email, string password, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, UserName>> GetNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
}

public sealed record AuthenticatedUser(Guid Id, string Email, IReadOnlyList<string> Roles);
public sealed record UserName(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(AuthenticatedUser user);
}

/// <summary>The caller of the current request, read from the JWT.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}

public static class Roles
{
    public const string Player = "Player";
    public const string Admin = "Admin";
}
