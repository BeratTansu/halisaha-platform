using System.Text.Json;
using Halisaha.Application.Common;
using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Halisaha.Infrastructure.Persistence;

public sealed class AdminSeedOptions
{
    public const string Section = "Seed:Admin";
    public string? Email { get; set; }

    /// <summary>Read from user-secrets or an environment variable (Seed__Admin__Password). Never committed.</summary>
    public string? Password { get; set; }
}

/// <summary>Applies migrations and seeds reference data, roles and the first admin. Every step is idempotent.</summary>
public sealed class DatabaseInitializer(
    AppDbContext db, RoleManager<IdentityRole<Guid>> roles, UserManager<AppUser> users,
    IOptions<AdminSeedOptions> adminOptions, TimeProvider clock, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedProvincesAsync(ct);
        await SeedRolesAsync();
        await SeedAdminAsync();
    }

    private async Task SeedProvincesAsync(CancellationToken ct)
    {
        if (await db.Provinces.AnyAsync(ct)) return;

        await using var stream = typeof(DatabaseInitializer).Assembly
            .GetManifestResourceStream("Halisaha.Infrastructure.Persistence.SeedData.provinces.json")
            ?? throw new InvalidOperationException("Province seed data is missing.");
        var data = await JsonSerializer.DeserializeAsync<SeedFile>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
            ?? throw new InvalidOperationException("Province seed data is empty.");

        foreach (var p in data.Provinces)
        {
            db.Provinces.Add(new Province(p.Id, p.Name));
            // Stable ids: province plate code * 1000 + position, e.g. 34001 for the first district of İstanbul.
            for (var i = 0; i < p.Districts.Count; i++)
                db.Districts.Add(new District(p.Id * 1000 + i + 1, p.Id, p.Districts[i]));
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Provinces} provinces", data.Provinces.Count);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in new[] { Roles.Player, Roles.Admin })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
    }

    /// <summary>Creates the first admin only when no admin exists. Fails fast if the password is not configured.</summary>
    private async Task SeedAdminAsync()
    {
        if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count > 0) return;

        var o = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(o.Email) || string.IsNullOrWhiteSpace(o.Password))
            throw new InvalidOperationException(
                "No admin exists and Seed:Admin:Email / Seed:Admin:Password are not configured. " +
                "Set them with 'dotnet user-secrets' or environment variables.");

        var admin = new AppUser
        {
            Id = Guid.NewGuid(), UserName = o.Email, Email = o.Email, EmailConfirmed = true,
            FirstName = "Admin", LastName = "Halısaha", CreatedAt = clock.GetUtcNow(),
        };
        var result = await users.CreateAsync(admin, o.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Admin seed failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Seeded the first admin user");
    }

    private sealed record SeedFile(List<SeedProvince> Provinces);
    private sealed record SeedProvince(int Id, string Name, List<string> Districts);
}
