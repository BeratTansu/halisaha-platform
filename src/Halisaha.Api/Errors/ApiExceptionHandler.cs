using System.Text.Json;
using FluentValidation;
using Halisaha.Api.Authentication;
using Halisaha.Application.Errors;
using Halisaha.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Halisaha.Api.Errors;

/// <summary>
/// Exception → HTTP cevabı eşlemesinin tek yeri (Karar 10, B4). Gövde her zaman RFC 9457 ProblemDetails ve
/// bir <c>code</c> alanı taşır. Bilinmeyen exception'ın ayrıntısı dışarı sızmaz.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // İstemci bağlantıyı kesti: yazılacak bir cevap yok, bu bir sunucu hatası değil.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by the client.");
            return true;
        }

        var details = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            // Yalnızca bilinen türler kendi kodu ve mesajıyla döner. Bilinmeyen bir tür (enum'a eklenip burada
            // eşlenmemiş bir değer) bizim hatamızdır: mesajı ve kodu dışarı verilmez, genel 500 olur.
            DomainException domain when StatusFor(domain.Kind) is { } status => ApiProblems.Domain(domain, status),
            _ => UnexpectedProblem(),
        };

        // .NET 10'da middleware, handler'ın işlediği exception'ı kendisi loglamaz; seviye burada seçilir.
        // 4xx kullanıcının isteğinden kaynaklanır (Information), 5xx bizim hatamızdır (Error, exception ile).
        if (details.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing the request.");
        }
        else
        {
            logger.LogInformation("Request rejected with {StatusCode} {ErrorCode}.", details.Status, details.Extensions["code"]);
        }

        httpContext.Response.StatusCode = details.Status!.Value;

        // RFC 9110: 401 cevabı WWW-Authenticate taşımalı. Bu yoldan iki 401 gelir (değer: BearerChallenge):
        // - auth.unauthenticated: token geçerliydi ama sahibi sistemde yok -> token reddedildi, invalid_token.
        // - auth.invalid_credentials (login): istekte token yok -> yalnızca "Bearer".
        // Token'ın kendisi geçersizse 401'i JwtBearer olayları yazar (JwtBearerProblemEvents), buraya gelmez.
        if (details.Status == StatusCodes.Status401Unauthorized)
        {
            var tokenWasRejected = exception is DomainException { Code: AuthErrors.UnauthenticatedCode };
            httpContext.Response.Headers.WWWAuthenticate = BearerChallenge.HeaderValue(tokenWasRejected);
        }

        // Exception bilerek ProblemDetailsContext'e verilmez: gövdeye hiçbir koşulda exception bilgisi girmesin.
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
        });
    }

    private static ValidationProblemDetails ValidationProblem(ValidationException exception)
    {
        // Alan adları command'dan (FirstName) gelir; JSON sözleşmesi camelCase (firstName). Çeviri burada,
        // API'nin işi; FluentValidation'ın global ayarı kullanılmaz.
        var errors = exception.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return ApiProblems.Validation(errors);
    }

    private static int? StatusFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation => StatusCodes.Status400BadRequest,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        DomainErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        // L5: kilitli hesap. Retry-After yok (kilidin bitiş zamanını taşımak exception'a veri alanı gerektirirdi);
        // IP rate limit'in 429'u (system.rate_limited) buraya gelmez; Retry-After'ıyla AuthRateLimiting yazar.
        DomainErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => null,
    };

    private static ProblemDetails UnexpectedProblem()
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
        };
        details.Extensions["code"] = "system.unexpected";
        return details;
    }
}
