using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Halisaha.Api.Errors;

/// <summary>
/// İstek gövdesi okunamadığında (bozuk JSON, tanımsız enum metni, yanlış tipte değer) [ApiController]'ın
/// otomatik 400 cevabını aynı biçime getirir: <c>code: validation.failed</c>, camelCase alan anahtarları.
/// Ham ModelState'te üç sorun var (6 Kasım'da WebApplicationFactory ile görüldü):
/// (1) her hataya ek olarak "request" anahtarı ("The request field is required.") eklenir; bu bir alan değil,
///     gövdenin okunamamasının sonucudur.
/// (2) anahtarlar JSON yoludur ("$.preferredPosition", kök için "$" veya "").
/// (3) mesajlar iç tip adlarını ve bayt konumlarını içerir ("...converted to Halisaha.Api.Contracts...").
/// </summary>
internal static class ModelBindingProblems
{
    // Bu katmanda gövde yalnızca bir action parametresine bağlanır; parametre adı controller'dakiyle aynı.
    private const string BodyParameterName = "request";
    private const string BodyKey = "body";

    public static IActionResult Create(ActionContext context)
    {
        var normalized = new ModelStateDictionary();

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0 || key == BodyParameterName)
            {
                continue;
            }

            if (key is "" or "$")
            {
                normalized.AddModelError(BodyKey, "The request body is missing or is not valid JSON.");
            }
            else
            {
                // "$.preferredPosition" → "preferredPosition": istemcinin gönderdiği ad, zaten camelCase.
                normalized.AddModelError(key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key, "The value is not valid.");
            }
        }

        // ProblemDetailsFactory: type, title ve traceId diğer 400'lerle (ApiExceptionHandler) aynı kurallarla gelir.
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = factory.CreateValidationProblemDetails(context.HttpContext, normalized, StatusCodes.Status400BadRequest);
        details.Extensions["code"] = ApiProblems.ValidationFailedCode;

        return new BadRequestObjectResult(details) { ContentTypes = { "application/problem+json" } };
    }
}
