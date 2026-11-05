using Halisaha.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Persistence;

public static class SeedingExtensions
{
    /// <summary>
    /// Referans verisi seed'ini bağlar. Hem AddInfrastructure hem test fixture'ı bunu çağırır; seeding tek yerde
    /// tanımlı kalır. EF araçları ve migration bundle'ları yalnızca sync delegate'i kullandığı için ikisi de şarttır.
    /// Seed yalnızca Migrate / EnsureCreated / dotnet ef database update / bundle ile çalışır; harici bir SQL script
    /// ile yapılan deploy'da çalışmaz.
    /// </summary>
    public static TBuilder UseHalisahaSeeding<TBuilder>(this TBuilder builder)
        where TBuilder : DbContextOptionsBuilder
    {
        builder.UseSeeding((context, _) => LocationSeeder.Seed(context));
        builder.UseAsyncSeeding((context, _, cancellationToken) => LocationSeeder.SeedAsync(context, cancellationToken));
        return builder;
    }
}
