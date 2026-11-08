using System.Text.Json.Serialization;
using Halisaha.Api.Authentication;
using Halisaha.Api.Errors;
using Halisaha.Api.OpenApi;
using Halisaha.Api.RateLimiting;
using Halisaha.Application;
using Halisaha.Infrastructure;
using Halisaha.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// DI grafiği her ortamda uygulama açılırken doğrulanır (varsayılan yalnızca Development'ta): eksik bir kayıt
// ilk istekte 500 olarak değil, başlangıçta hata olarak görünür. Connection string kontrolüyle aynı ilke.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Yerel geliştirme: appsettings.Development.json. Production: ConnectionStrings__Halisaha ortam değişkeni.
var connectionString = builder.Configuration.GetConnectionString("Halisaha");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Halisaha' is not configured. " +
        "Set ConnectionStrings:Halisaha (Development: appsettings.Development.json, other environments: the ConnectionStrings__Halisaha environment variable).");
}

// Composition root: Api, Application ve Infrastructure'ı yalnızca burada birbirine bağlar.
// Controller'lar Application tiplerini kullanır; Infrastructure tipleri burada (AddInfrastructure) kalır.
builder.Services.AddApplication();
// JWT ayarları (Karar 11, L6): issuer/audience/süre appsettings.json'da; anahtar repoda değil (Development:
// user-secrets, diğer ortamlar: Jwt__SigningKey). Kurallar Infrastructure'da, uygulama açılırken doğrulanır.
builder.Services.AddInfrastructure(
    connectionString, jwt => builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwt));

// Uygulamanın saati (Karar 7): handler'lar "şimdi"yi buradan alır; testler sahte saat verebilir.
builder.Services.AddSingleton(TimeProvider.System);

// Kimlik doğrulama: JWT bearer (Karar 11). Doğrulama parametreleri token'ı üreten tarafla aynı JwtOptions'tan,
// aynı yerden (JwtTokenValidation) kurulur; iki taraf ayrı ayar okuyamaz. MapInboundClaims kapalı: claim'ler
// token'daki adıyla gelir ("sub" ClaimTypes.NameIdentifier'a dönüşmez; 6 Kasım'da ölçüldü, varsayılan açıktı).
// Olaylar: sub kontrolü, 401/403 gövdeleri (code + traceId) ve RFC 6750 başlığı (JwtBearerProblemEvents).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = JwtTokenValidation.CreateParameters(jwt.Value);
        bearer.Events = JwtBearerProblemEvents.Create();
    });

// Fallback policy: kendi yetki kuralı olmayan her endpoint kimliği doğrulanmış kullanıcı ister. [Authorize]
// unutulursa endpoint herkese açık kalmaz, 401 döner. Anonim endpoint'ler (register, login) [AllowAnonymous] ile
// açıkça işaretlenir. Ölçüldü: eşleşmeyen adres de bu kurala girer; anonim istek 401, giriş yapmış istek 404
// alır. Anonim istemci hangi adreslerin var olduğunu öğrenemez (AuthPipelineTests).
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Kayıt ve girişin IP başına limiti (Dilim 5): kurallar ve bilinen sınırları AuthRateLimiting'de.
builder.Services.AddAuthRateLimiting();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Enum'lar JSON'da metin ("Goalkeeper"). Sayı kabul edilmez: sözleşmede tek gösterim olsun.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)))
    .ConfigureApiBehaviorOptions(options =>
        // Model binding hataları (bozuk JSON, tanımsız enum metni) da aynı 400 biçiminde dönsün.
        options.InvalidModelStateResponseFactory = ModelBindingProblems.Create);

// OpenAPI dokümanı da enum'ları adıyla ("Forward") göstersin: doküman üreticisi controller ayarını (AddJsonOptions) okumuyor.
// Bu satır olmasaydı Scalar'da "preferredPosition": 1 gibi bir örnek görünür ve onu gönderen 400 hatası alırdı.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// OpenAPI dokümanı (/openapi/v1.json) + Bearer şeması: Scalar token yapıştırma alanını buradan bilir.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecurityTransformer>();
    options.AddOperationTransformer<BearerSecurityTransformer>();
});

var app = builder.Build();

// En başta: sonraki middleware'lerde ve controller'larda fırlayan her exception ApiExceptionHandler'a gelir.
app.UseExceptionHandler();

// API haritası yalnızca Development'ta: production'da hangi endpoint'lerin olduğu dışarı açılmaz. Fallback policy
// her endpoint'e giriş şartı koyduğu için ikisi de açıkça anonim işaretlenir (yoksa sayfa 401 verirdi).
// Scalar arayüzü: /scalar. Bearer varsayılan seçili: token'ı Authentication alanına yapıştırmak yeter.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.AddPreferredSecuritySchemes(BearerSecurityTransformer.SchemeName))
        .AllowAnonymous();
}

app.UseHttpsRedirection();

// Routing'den sonra (WebApplication UseRouting'i en başa kendisi ekler; isimli politika endpoint'i bilmeli) ve
// kimlik doğrulamadan önce: limiti aşan istek token doğrulamasına ve controller'a hiç ulaşmaz.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
