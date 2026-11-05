using Halisaha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure servislerini kaydeder. Connection string'in nereden geldiğini çağıran bilir
    /// (Api: configuration, testler: Testcontainers); Infrastructure yalnızca onunla ne yapılacağını bilir.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Scoped (varsayılan) bilinçli: Register'da UserManager ile UnitOfWork aynı context örneğini paylaşmalı.
        // EnableRetryOnFailure eklenmez: retry stratejisi ExecuteInTransactionAsync'in açtığı transaction'larla çalışmaz.
        services.AddDbContext<HalisahaDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
