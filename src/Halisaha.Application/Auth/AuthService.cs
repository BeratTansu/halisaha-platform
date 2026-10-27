using FluentValidation;
using Halisaha.Application.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Auth;

public sealed record RegisterRequest(
    string Email, string Password, string FirstName, string LastName, string? PhoneNumber,
    Position PreferredPosition, int SkillLevel, int PreferredDistrictId);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(Guid UserId, string Token, DateTimeOffset ExpiresAt, IReadOnlyList<string> Roles);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).Matches(@"^\+?[0-9 ]+$").When(x => !string.IsNullOrEmpty(x.PhoneNumber));
        RuleFor(x => x.PreferredPosition).IsInEnum();
        RuleFor(x => x.SkillLevel).InclusiveBetween(1, 5);
        RuleFor(x => x.PreferredDistrictId).GreaterThan(0);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class AuthService(
    IApplicationDbContext db,
    IIdentityService identity,
    ITokenService tokens,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator)
{
    /// <summary>
    /// Creates the identity user and the player profile in one transaction:
    /// a user without a profile, or a profile without a user, never exists.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        if (!await db.Districts.AnyAsync(d => d.Id == request.PreferredDistrictId, ct))
            throw new NotFoundException("District", request.PreferredDistrictId);

        await using var tx = await db.BeginTransactionAsync(ct);
        var userId = await identity.CreateUserAsync(
            request.Email, request.Password, request.FirstName.Trim(), request.LastName.Trim(), request.PhoneNumber, ct);

        db.PlayerProfiles.Add(new PlayerProfile(userId, request.PreferredPosition, request.SkillLevel, request.PreferredDistrictId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await LoginAsync(new LoginRequest(request.Email, request.Password), ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);
        var user = await identity.CheckCredentialsAsync(request.Email, request.Password, ct)
            ?? throw new InvalidCredentialsException();
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(user.Id, token, expiresAt, user.Roles);
    }
}
