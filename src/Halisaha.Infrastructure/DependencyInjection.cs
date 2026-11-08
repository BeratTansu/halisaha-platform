using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Infrastructure.Authentication;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Persistence.Queries;
using Halisaha.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Halisaha.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure servislerini kaydeder. Connection string'in ve JWT ayarlarının nereden geldiğini çağıran bilir
    /// (Api: configuration, testler: Testcontainers ve kendi test anahtarları); Infrastructure yalnızca onlarla ne
    /// yapılacağını bilir.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, Action<JwtOptions> configureJwt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configureJwt);

        // Scoped (varsayılan) bilinçli: Register'da UserManager ile UnitOfWork aynı context örneğini paylaşmalı.
        // EnableRetryOnFailure eklenmez: retry stratejisi ExecuteInTransactionAsync'in açtığı transaction'larla çalışmaz.
        services.AddDbContext<HalisahaDbContext>(options => options.UseNpgsql(connectionString).UseHalisahaSeeding());

        // Context ile aynı ömür: UnitOfWork, request scope'undaki context'in transaction'ını yönetir.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<ILocationQueries, LocationQueries>();
        services.AddScoped<IUserQueries, UserQueries>();

        // AddIdentity değil AddIdentityCore: cookie kimlik doğrulaması eklemez (JWT kullanılır). Roller eklenmez
        // (Player rolü yok; Admin kendi diliminde). AddEntityFrameworkStores store'u scoped kaydeder ve DI'daki
        // HalisahaDbContext'i verir: UserManager'ın yazmaları UnitOfWork'ün transaction'ına katılır.
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

                // L4: 5 başarısız deneme -> 5 dk kilit. Varsayılanla aynı değerler, ama açıkça yazılır: karar
                // burada okunur ve bir paket güncellemesi varsayılanı değiştirse bile davranış değişmez.
                // 5. yanlış deneme kilidi koyar ve kendisi de 429 alır (Identity sayacı sıfırlar, LockoutEnd yazar).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                // Login akışı bu üçünün kapalı olmasına dayanır. Biri açılırsa SignInManager parolayı kontrol
                // etmeden NotAllowed döner; IdentityService bunu 500'e çevirir. Açmak bilinçli bir tasarım işidir:
                // NotAllowed parola doğrulanmadan önce dönerse hesabın varlığını ele verir (Karar 11).
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<HalisahaDbContext>()
            // Yalnızca CheckPasswordSignInAsync kullanılır (kilit kontrolü, sayaç, eşzamanlılıkta reddetme);
            // SignInAsync/cookie kullanılmaz. Microsoft'un davranışını kopyalamak yerine kendisi (L4).
            .AddSignInManager();
        services.AddScoped<IIdentityService, IdentityService>();

        // SignInManager'ın constructor'ı IAuthenticationSchemeProvider ister (2FA "beni hatırla" şeması var mı
        // diye bakar). Kaydı burada: Infrastructure kendi bağımlılığını kendisi kurar, testler üretimle aynı DI'ı
        // kullanır. Şema eklemez; JwtBearer şemasını ve varsayılanı Api (composition root) ekler. Tekrar
        // çağrılabilir (TryAdd), Api'deki AddAuthentication(...) ile çakışmaz.
        services.AddAuthentication();

        // JWT (Karar 11, L6). ValidateOnStart: host altında ayar bozuksa uygulama açılmaz; aksi halde hata ilk
        // login'de (kısa anahtar: IDX10720) ya da her doğrulamada (anahtar yok: herkes 401) ortaya çıkardı.
        // Host dışında validator, IOptions<JwtOptions>.Value'ya ilk erişimde çalışır.
        services.AddOptions<JwtOptions>().Configure(configureJwt).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        // Singleton: durum taşımaz; bağımlılıkları (IOptions, TimeProvider) da singleton.
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
