using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Adapters;

namespace SmartParking.PaymentService.Tests.Integration;

public class DependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_ShouldResolveAllRequiredDependencies_WithoutExceptions()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payment"] = "Host=localhost;Port=5444;Database=smartpark_payment;Username=postgres;Password=postgres",
                ["Services:ReservationService:BaseUrl"] = "http://localhost:5002/"
            })
            .Build();

        services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddPersistenceServices(configuration);

        var provider = services.BuildServiceProvider();

        // 1. Domain Engine
        var calculator = provider.GetService<IParkingPricingCalculator>();
        Assert.NotNull(calculator);

        // 2. MediatR
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);

        // 3. Validator
        var validator = provider.GetService<IValidator<CalculateParkingFeeQuery>>();
        Assert.NotNull(validator);

        // 4. CQRS Handler
        var handler = provider.GetService<IRequestHandler<CalculateParkingFeeQuery, PricingCalculationResult>>();
        Assert.NotNull(handler);

        // 5. Hexagonal Ports & Persistence Adapters
        var tariffPort = provider.GetService<IParkingTariffPort>();
        Assert.NotNull(tariffPort);
        Assert.IsType<TariffPersistenceAdapter>(tariffPort);

        var reservationPort = provider.GetService<IReservationPricingSnapshotPort>();
        Assert.NotNull(reservationPort);
    }
}
