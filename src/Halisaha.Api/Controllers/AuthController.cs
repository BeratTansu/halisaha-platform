using Halisaha.Api.Contracts.Auth;
using Halisaha.Application.Commands.Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// Yeni oyuncu kaydı. 201 + userId döner; Location header'ı yok (kullanıcıyı getiren bir GET endpoint'i
    /// henüz yok, var olmayan bir adrese işaret edilmez).
    /// </summary>
    // Handler constructor'dan değil action parametresinden alınır: Login eklendiğinde her istek yalnızca
    // kendi handler'ını oluşturur.
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterRequest request, [FromServices] RegisterHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(result.UserId));
    }
}
