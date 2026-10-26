using UserService.API.Services;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.API.Extensions;

public static class ApiCurrentActorExtensions
{
    /// <summary>
    /// Supplies the Application layer with the authenticated caller of the current request.
    /// </summary>
    /// <remarks>
    /// Every host that composes the Application layer must register the port: the handlers
    /// fail fast at start-up when the current actor is missing, rather than treating an
    /// unidentified caller as a permitted one.
    /// </remarks>
    public static IServiceCollection AddApiCurrentActor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        return services;
    }
}
