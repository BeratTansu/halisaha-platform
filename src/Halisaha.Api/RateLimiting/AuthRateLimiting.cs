using System.Globalization;
using System.Threading.RateLimiting;
using Halisaha.Api.Errors;
using Halisaha.Application.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Halisaha.Api.RateLimiting;

/// <summary>
/// Kayıt ve girişin IP başına limiti (Dilim 5). Hesap kilidi (5 yanlış deneme) tek hesabı korur; bu limit, aynı
/// IP'den çok sayıda farklı e-posta denenmesine ve kilit sayacının paralel denemelerde eksik saymasına karşıdır.
/// İkisi birbirinin yerine geçmez.
/// <list type="bullet">
/// <item>Register ve login tek politikayı, dolayısıyla aynı kotayı paylaşır. Başarılı ya da başarısız fark etmez:
/// her deneme kotadan düşer.</item>
/// <item>Fixed window (bilinçli): pencere sınırında kısa sürede iki pencerenin kotası (2 x limit) geçebilir. MVP için
/// kabul. Sliding window'a geçmek SegmentsPerWindow ayarı ve testlerin güncellenmesini gerektirir.</item>
/// <item>Sayaçlar bu sürecin belleğinde: API birden fazla kopya çalışırsa her kopya kendi sayacını tutar. Kova sayısı
/// gelen IP sayısıyla büyür; boşta kalan kovalar temizlenir (.NET'in DefaultPartitionedRateLimiter'ı, 10 sn boşta
/// sınırı; 8 Kasım'da kontrol edildi), ama çok sayıda IP'den gelen saldırı (DDoS) bu katmanın değil edge/WAF'ın işi.</item>
/// <item>Canlıya çıkış şartı: API bir proxy (nginx vb.) arkasında ForwardedHeaders olmadan çalışırsa
/// RemoteIpAddress proxy'nin adresi olur ve bütün kullanıcılar tek kovaya düşer.</item>
/// </list>
/// </summary>
internal static class AuthRateLimiting
{
    public const string PolicyName = "auth";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        // Geçersiz limit (0, negatif, sayı olmayan metin) uygulamayı açtırmaz: JwtOptions ile aynı fail-fast ilkesi.
        services.AddOptions<AuthRateLimitOptions>()
            .BindConfiguration(AuthRateLimitOptions.SectionName)
            .Validate(options => options.PermitLimit >= 1, $"{AuthRateLimitOptions.SectionName}:PermitLimit must be at least 1.")
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = OnRejectedAsync;
            limiter.AddPolicy(PolicyName, httpContext =>
            {
                var permitLimit = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value.PermitLimit;
                return RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey.From(httpContext.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = Window,
                        QueueLimit = 0, // bekletme yok: limit dolunca hemen 429
                    });
            });
        });

        return services;
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        // Middleware Retry-After yazmaz; limiter'ın bildirdiği süre burada başlığa çevrilir. Yukarı yuvarlanır:
        // 2,4 sn bekleyecek istemciye "2" denirse erken gelir ve yine 429 alır.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        // Debug ve IP'siz: saldırı altında saniyede binlerce red log'u doldurmasın, log'a kişisel veri girmesin.
        // Asıl ölçüm ASP.NET Core'un rate limiting metrikleri.
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AuthRateLimiting).FullName!)
            .LogDebug("Rate limit rejected a request to {Path}.", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ApiProblems.Domain(SystemErrors.RateLimited(), StatusCodes.Status429TooManyRequests),
        });
    }
}
