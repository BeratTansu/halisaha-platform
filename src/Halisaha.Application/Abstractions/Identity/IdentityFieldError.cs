namespace Halisaha.Application.Abstractions.Identity;

/// <summary>
/// Kimlik sisteminin reddedebileceği girdi alanları. String değil enum: adapter, command'ın property adlarını
/// bilmek zorunda kalmaz; alanı command'daki adla eşlemek handler'ın işidir.
/// </summary>
public enum IdentityField
{
    Email,
    Password,
}

public sealed record IdentityFieldError(IdentityField Field, string Message);
