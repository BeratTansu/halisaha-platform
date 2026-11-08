using FluentValidation;
using Halisaha.Application.Commands.Login;
using Halisaha.Application.Commands.Register;
using Halisaha.Application.Queries.GetMe;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Use case'leri ve validator'larını kaydeder. Kayıtlar elle yazılır (assembly taraması yok): kaç use case
    /// olduğu tek bakışta görünür ve FluentValidation'ın DI paketine gerek kalmaz.
    /// Port'ların uygulamaları (Infrastructure) ve TimeProvider (composition root) burada değil.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: handler scoped port'lara (UoW, repository) bağımlı. Validator durumsuz, ama ileride bir
        // scoped servis isterse yakalanma (captive dependency) olmasın diye o da scoped.
        services.AddScoped<IValidator<RegisterCommand>, RegisterCommandValidator>();
        services.AddScoped<RegisterHandler>();

        services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
        services.AddScoped<LoginHandler>();

        services.AddScoped<GetMeHandler>();

        return services;
    }
}
