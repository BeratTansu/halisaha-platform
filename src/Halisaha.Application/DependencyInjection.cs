using FluentValidation;
using Halisaha.Application.Auth;
using Halisaha.Application.Locations;
using Halisaha.Application.Matches;
using Halisaha.Application.Profiles;
using Halisaha.Application.Venues;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<LocationService>();
        services.AddScoped<VenueService>();
        services.AddScoped<MatchService>();
        services.AddScoped<MatchRequestService>();
        services.AddScoped<MatchResultService>();
        return services;
    }
}
