using System.Security.Claims;
using Halisaha.Infrastructure.Authentication;

namespace Halisaha.Api.Authentication;

/// <summary>
/// L2b: kimliği token'dan okuyan tek yer. Controller'lar <c>User.GetActorId()</c> ile alır ve command/query'ye
/// <c>ActorId</c> olarak verir; Application token'ı ya da HttpContext'i görmez.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// HTTP cevabı üretmez. <c>sub</c>'ın geçerli bir Guid olduğu kimlik doğrulama sırasında kontrol edilir
    /// (<see cref="JwtBearerProblemEvents"/>); geçersiz token buraya hiç ulaşmaz. Burada bulunamaması bizim
    /// hatamızdır (ör. <c>[AllowAnonymous]</c> bir endpoint'te çağrılması): 500 olarak görünür kalır.
    /// </summary>
    public static Guid GetActorId(this ClaimsPrincipal user)
    {
        var subject = user.FindFirst(JwtTokenValidation.SubjectClaimType)?.Value;
        if (Guid.TryParse(subject, out var actorId) && actorId != Guid.Empty)
        {
            return actorId;
        }

        throw new InvalidOperationException(
            "GetActorId was called for a request without an authenticated user. " +
            "Use it only on endpoints that require authentication.");
    }
}
