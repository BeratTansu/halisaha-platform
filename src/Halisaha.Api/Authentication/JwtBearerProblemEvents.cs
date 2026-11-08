using Halisaha.Api.Errors;
using Halisaha.Application.Errors;
using Halisaha.Domain.Common;
using Halisaha.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Halisaha.Api.Authentication;

/// <summary>
/// JwtBearer'ın üç olayı (Karar 11, Dilim 3):
/// <list type="number">
/// <item><see cref="TokenValidated"/>: imza, süre, issuer, audience geçtikten sonra <c>sub</c>'ın bir kullanıcı Id'si
/// (Guid) olduğu kontrol edilir. Token'ın şekli kimlik doğrulamanın işidir: bozuk <c>sub</c>'lı token hiçbir
/// controller'a ulaşmaz ve diğer geçersiz token'larla aynı 401'i alır.</item>
/// <item><see cref="Challenge"/>: varsayılan 401 gövdesizdir ve ApiExceptionHandler'a uğramaz (6 Kasım'da ölçüldü).
/// Burada ortak hata biçimiyle (problem+json, code, traceId) yazılır.</item>
/// <item><see cref="Forbidden"/>: 403 için aynısı.</item>
/// </list>
/// </summary>
internal static class JwtBearerProblemEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = TokenValidated,
        OnChallenge = Challenge,
        OnForbidden = Forbidden,
    };

    private static Task TokenValidated(TokenValidatedContext context)
    {
        // Kendi ürettiğimiz token'da sub her zaman Guid'dir; yine de claim dışarıdan gelen bir girdidir.
        var subject = context.Principal?.FindFirst(JwtTokenValidation.SubjectClaimType)?.Value;
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
        {
            // Fail: kimlik doğrulama başarısız sayılır, AuthenticateFailure dolar; challenge invalid_token yazar.
            context.Fail("The token's subject is not a valid user id.");
        }

        return Task.CompletedTask;
    }

    private static async Task Challenge(JwtBearerChallengeContext context)
    {
        // HandleResponse: JwtBearer'ın kendi 401'ini (gövdesiz, error_description'lı başlık) yazmasını durdurur.
        // Yan etkisi: başlığı da yazmaz; aşağıda elle eklenir.
        context.HandleResponse();

        // AuthenticateFailure yalnızca bir token gönderilip reddedildiyse doludur; token yoksa null.
        var tokenWasRejected = context.AuthenticateFailure is not null;
        context.Response.Headers.WWWAuthenticate = BearerChallenge.HeaderValue(tokenWasRejected);

        await WriteAsync(context.HttpContext, AuthErrors.Unauthenticated(), StatusCodes.Status401Unauthorized);
    }

    private static Task Forbidden(ForbiddenContext context)
    {
        // RFC 6750'nin insufficient_scope'u OAuth scope'ları içindir; rol kontrolüne oturmaz, başlık eklenmez.
        return WriteAsync(context.HttpContext, AuthErrors.Forbidden(), StatusCodes.Status403Forbidden);
    }

    private static async Task WriteAsync(HttpContext httpContext, DomainException error, int status)
    {
        httpContext.Response.StatusCode = status;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ApiProblems.Domain(error, status),
        });
    }
}
