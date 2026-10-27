namespace Halisaha.Application.Common;

/// <summary>The requested record does not exist (404).</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>Wrong email or password (401). The message never says which one was wrong.</summary>
public sealed class InvalidCredentialsException() : Exception("Email or password is incorrect.");

/// <summary>Identity rejected the data, e.g. a duplicate email or a weak password (400).</summary>
public sealed class IdentityValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("User data is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
