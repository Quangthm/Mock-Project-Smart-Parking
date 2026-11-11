using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Usecase.ReservationGate;
using SmartParking.OperationService.Application.Common.Behaviors;

namespace SmartParking.OperationService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        services.AddScoped<StructureSafetyService>();
        services.AddScoped<IPaymentSucceededHandler, PaymentSucceededHandler>();
        return services;
    }
}
