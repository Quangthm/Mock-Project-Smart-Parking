using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.PaymentService.Application.Common.Behaviors;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CalculateParkingFeeQuery).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(CalculateParkingFeeQueryValidator).Assembly);

        services.AddScoped<ReviewedPayOsSettlement>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IParkingPricingCalculator, ParkingPricingCalculator>();

        return services;
    }
}
