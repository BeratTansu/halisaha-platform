using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Test sunucusu (TestServer) istemci IP'si vermez; RemoteIpAddress null gelir. Bu ara katman testte istemci IP'sini
/// bir başlıktan ayarlar. Yalnızca test projesinde durur ve yalnızca <see cref="AddTo"/> ile eklenir: üretim kodunda
/// "istemci IP'sini başlıktan oku" diye bir yol yoktur, olmamalıdır (istemci kendi kovasını seçerdi).
/// </summary>
public static class TestClientIp
{
    public const string HeaderName = "X-Test-Client-Ip";

    public static void AddTo(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, StartupFilter>());

    public static HttpRequestMessage Post(string url, object body, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        request.Headers.Add(HeaderName, clientIp);
        return request;
    }

    public static HttpRequestMessage Get(string url, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(HeaderName, clientIp);
        return request;
    }

    // Startup filter, uygulamanın kendi middleware'lerinden önce çalışır: rate limiter IP'yi hazır bulur.
    private sealed class StartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out var value))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
