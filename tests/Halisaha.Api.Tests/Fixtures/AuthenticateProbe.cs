using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Pipeline'ın başına eklenen prob: uygulamanın kendi JwtBearer handler'ını doğrudan çağırır ve sonucu JSON olarak
/// yazar. Korunan bir endpoint henüz yokken "uygulama bu token'ı kabul eder mi" sorusunu gerçek handler'la
/// cevaplar. Uygulamanın kayıtları ve ayarları değişmez; yalnızca bu yol eklenir.
/// </summary>
public sealed class AuthenticateProbe : IStartupFilter
{
    public const string Path = "/__test/authenticate";

    public sealed record Result(bool Succeeded, bool None, string? Failure, string? Name, string[] ClaimTypes);

    /// <summary>Factory'ye <c>WithWebHostBuilder</c>/<c>CreateFactory</c> ile verilecek ayar adımı.</summary>
    public static void AddTo(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, AuthenticateProbe>());

    public static async Task<Result> AuthenticateAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Result>())!;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Map(Path, probe => probe.Run(async context =>
        {
            var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            await context.Response.WriteAsJsonAsync(new Result(
                result.Succeeded,
                result.None,
                result.Failure?.GetType().Name,
                result.Principal?.Identity?.Name,
                result.Principal?.Claims.Select(c => c.Type).ToArray() ?? []));
        }));
        next(app);
    };
}
