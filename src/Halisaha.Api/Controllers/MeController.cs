using Halisaha.Api.Authentication;
using Halisaha.Api.Contracts.Me;
using Halisaha.Application.Queries.GetMe;
using Microsoft.AspNetCore.Mvc;

namespace Halisaha.Api.Controllers;

/// <summary>
/// Giriş yapmış kullanıcının kendisi. Bilerek <c>[Authorize]</c> yok: korumayı fallback policy sağlar (her endpoint
/// varsayılan olarak giriş ister). Token yoksa ya da geçersizse istek buraya hiç ulaşmaz (401).
/// </summary>
[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    /// <summary>
    /// 200 + MeResponse. 401 auth.unauthenticated: token yok, geçersiz ya da sahibi sistemde yok (auth sözleşmesi,
    /// Karar 11). 404 yok: bu adres her zaman var.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Get([FromServices] GetMeHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetMeQuery(User.GetActorId()), cancellationToken);

        return Ok(MeResponse.From(result));
    }
}
