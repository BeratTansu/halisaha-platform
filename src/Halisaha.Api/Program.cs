using Halisaha.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Yerel geliştirme: appsettings.Development.json. Production: ConnectionStrings__Halisaha ortam değişkeni.
var connectionString = builder.Configuration.GetConnectionString("Halisaha");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Halisaha' is not configured. " +
        "Set ConnectionStrings:Halisaha (Development: appsettings.Development.json, other environments: the ConnectionStrings__Halisaha environment variable).");
}

builder.Services.AddInfrastructure(connectionString);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
