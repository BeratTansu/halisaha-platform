using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Persistence.Queries;
using Halisaha.Infrastructure.Persistence.Repositories;
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
        services.AddDbContext<HalisahaDbContext>(options => options.UseNpgsql(connectionString).UseHalisahaSeeding());

        // Context ile aynı ömür: UnitOfWork, request scope'undaki context'in transaction'ını yönetir.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<ILocationQueries, LocationQueries>();

        // AddIdentity değil AddIdentityCore: cookie kimlik doğrulaması ve SignInManager eklemez (JWT kullanılacak).
        // Roller eklenmez (Player rolü yok; Admin kendi diliminde). AddEntityFrameworkStores store'u scoped kaydeder
        // ve DI'daki HalisahaDbContext'i verir: UserManager'ın yazmaları UnitOfWork'ün transaction'ına katılır.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Normal durumda düzgün DuplicateEmail sonucu; yarıştaki garantiyi EmailIndex (unique) verir.
                options.User.RequireUniqueEmail = true;

                // C3: en az 8 karakter, en az bir rakam ve bir küçük harf. Politika yalnızca burada yaşar.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
            })
            .AddEntityFrameworkStores<HalisahaDbContext>();
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }
}
