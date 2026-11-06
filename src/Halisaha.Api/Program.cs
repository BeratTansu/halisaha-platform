using System.Text.Json.Serialization;
using Halisaha.Api.Errors;
using Halisaha.Application;
using Halisaha.Infrastructure;

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
builder.Services.AddInfrastructure(connectionString);

// Uygulamanın saati (Karar 7): handler'lar "şimdi"yi buradan alır; testler sahte saat verebilir.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Enum'lar JSON'da metin ("Goalkeeper"). Sayı kabul edilmez: sözleşmede tek gösterim olsun.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)))
    .ConfigureApiBehaviorOptions(options =>
        // Model binding hataları (bozuk JSON, tanımsız enum metni) da aynı 400 biçiminde dönsün.
        options.InvalidModelStateResponseFactory = ModelBindingProblems.Create);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// En başta: sonraki middleware'lerde ve controller'larda fırlayan her exception ApiExceptionHandler'a gelir.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
