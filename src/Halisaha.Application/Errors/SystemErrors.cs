using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Hiçbir iş kuralına ait olmayan hatalar. İstek doğrudur; başka bir işlemle çakıştığı ya da istemci limiti aştığı
/// için reddedilmiştir ve (gerekirse bekledikten sonra) tekrar denenebilir.
/// </summary>
public static class SystemErrors
{
    public static DomainException ConcurrencyConflict() =>
        DomainException.Conflict(
            "system.concurrency_conflict",
            "The operation conflicted with another operation running at the same time. Please try again.");

    /// <summary>
    /// Aynı istemciden kısa sürede çok istek geldi (IP rate limit, Dilim 5). Hesap kilidinden (auth.locked_out)
    /// ayrı kod: istemci 429'un sebebini code alanından ayırt eder. Bekleme süresi Retry-After başlığında.
    /// </summary>
    public static DomainException RateLimited() =>
        DomainException.TooManyRequests(
            "system.rate_limited",
            "Too many requests from this client. Please wait before trying again.");
}
