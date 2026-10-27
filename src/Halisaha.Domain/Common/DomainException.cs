namespace Halisaha.Domain.Common;

/// <summary>
/// Thrown when an operation breaks a business rule.
/// The API maps <see cref="DomainErrorKind"/> to an HTTP status code.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainErrorKind Kind { get; }
    public string Code { get; }

    public DomainException(DomainErrorKind kind, string code, string message) : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public static DomainException Validation(string code, string message) => new(DomainErrorKind.Validation, code, message);
    public static DomainException Conflict(string code, string message) => new(DomainErrorKind.Conflict, code, message);
    public static DomainException Forbidden(string code, string message) => new(DomainErrorKind.Forbidden, code, message);
}

public enum DomainErrorKind
{
    /// <summary>The request itself is invalid (400).</summary>
    Validation,
    /// <summary>The request is valid but conflicts with the current state (409).</summary>
    Conflict,
    /// <summary>The caller is known but not allowed to do this (403).</summary>
    Forbidden,
}
