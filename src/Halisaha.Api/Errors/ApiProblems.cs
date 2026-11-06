using Microsoft.AspNetCore.Mvc;

namespace Halisaha.Api.Errors;

/// <summary>
/// 400 doğrulama cevabının tek biçimi. Üç kaynak (validator, Identity'nin reddi, model binding) bu biçimde
/// döner; istemci hatanın hangi katmandan geldiğini bilmez.
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
}
