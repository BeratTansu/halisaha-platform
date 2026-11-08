using Halisaha.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Halisaha.Api.Errors;

/// <summary>
/// Hata gövdelerinin tek biçimi. 400: üç kaynak (validator, Identity'nin reddi, model binding) aynı biçimde
/// döner; istemci hatanın hangi katmandan geldiğini bilmez. Domain hataları (409, 401, 403...): hem
/// ApiExceptionHandler hem JwtBearer olayları buradan üretir, iki yol farklı gövde yazamaz.
/// </summary>
internal static class ApiProblems
{
    public const string ValidationFailedCode = "validation.failed";

    public static ValidationProblemDetails Validation(IDictionary<string, string[]> errors)
    {
        var details = new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
        details.Extensions["code"] = ValidationFailedCode;
        return details;
    }

    /// <summary>Kod ve mesaj hatanın kendisinden; traceId'yi ProblemDetails servisi ekler.</summary>
    public static ProblemDetails Domain(DomainException error, int status)
    {
        var details = new ProblemDetails { Status = status, Detail = error.Message };
        details.Extensions["code"] = error.Code;
        return details;
    }
}
