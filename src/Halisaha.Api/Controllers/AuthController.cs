using Halisaha.Api.Contracts.Auth;
using Halisaha.Application.Commands.Login;
using Halisaha.Application.Commands.Register;
using Microsoft.AspNetCore.Authorization;
using Halisaha.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Halisaha.Api.Controllers;

/// <summary>
/// Kimlik endpoint'leri. Controller iş mantığı taşımaz: isteği command'a çevirir, handler'ı çağırır, sonucu
/// HTTP cevabına çevirir. Hatalar burada yakalanmaz; ApiExceptionHandler tek yerden çevirir.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Yeni oyuncu kaydı + otomatik giriş (L7). IP limiti login ile ortak (429 system.rate_limited). 201 + AuthResponse (login ile aynı gövde); Location header'ı yok
    /// (kullanıcıyı getiren adres /api/me, oluşturulan kaynağın kendi adresi değil).
    /// </summary>
    // Handler constructor'dan değil action parametresinden alınır: her istek yalnızca kendi handler'ını oluşturur.
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request, [FromServices] RegisterHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);

        // Token taşıyan cevap hiçbir önbellekte (tarayıcı, proxy) saklanmaz (RFC 6749 5.1).
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status201Created, AuthResponse.From(result));
    }

    /// <summary>
    /// E-posta ve parolayla giriş (L3, L4, L7). 200 + AuthResponse. Hatalar: 400 validation.failed (boş alan),
    /// 401 auth.invalid_credentials (kayıtsız e-posta ve yanlış parola aynı), 429 auth.locked_out (hesap kilidi) ya da
    /// system.rate_limited (IP limiti, register ile ortak).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request, [FromServices] LoginHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);

        // Token taşıyan cevap hiçbir önbellekte (tarayıcı, proxy) saklanmaz (RFC 6749 5.1).
        Response.Headers.CacheControl = "no-store";
        return Ok(AuthResponse.From(result));
    }
}
