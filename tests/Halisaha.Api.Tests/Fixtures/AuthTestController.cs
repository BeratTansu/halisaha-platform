using Halisaha.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Yalnızca test projesinde duran endpoint'ler: uygulamada henüz rol isteyen bir endpoint yok (403) ve fallback
/// policy'yi kendi yetki kuralı olmayan bir action üzerinden kanıtlamak gerekiyor. Uygulamaya yalnızca
/// <see cref="AddTo"/> ile eklenir; diğer test host'larında ve üretimde yoktur.
/// </summary>
[ApiController]
[Route(BasePath)]
public sealed class AuthTestController : ControllerBase
{
    public const string BasePath = "__test/auth";

    public static void AddTo(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly));

    /// <summary>Hiçbir yetki attribute'u yok: korumayı yalnızca fallback policy sağlar.</summary>
    [HttpGet("default")]
    public IActionResult Default() => Ok(new { actorId = User.GetActorId() });

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult Admin() => Ok();

    /// <summary>GetActorId'nin anonim bir endpoint'te yanlış kullanımı: 500 olarak görünür kalmalı.</summary>
    [HttpGet("anonymous-actor")]
    [AllowAnonymous]
    public IActionResult AnonymousActor() => Ok(new { actorId = User.GetActorId() });
}
