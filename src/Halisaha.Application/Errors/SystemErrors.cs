using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Hiçbir iş kuralına ait olmayan, eşzamanlılıktan doğan hatalar. İstek doğrudur; aynı anda çalışan başka bir
/// işlemle çakıştığı için reddedilmiştir ve tekrar denenebilir.
/// </summary>
public static class SystemErrors
{
    public static DomainException ConcurrencyConflict() =>
        DomainException.Conflict(
            "system.concurrency_conflict",
            "The operation conflicted with another operation running at the same time. Please try again.");
}
