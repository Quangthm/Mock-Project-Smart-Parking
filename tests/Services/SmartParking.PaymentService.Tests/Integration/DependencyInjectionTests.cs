using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Persistence;

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
                ["Services:ParkingService:BaseUrl"] = "http://localhost:5001/",
                ["Services:ReservationService:BaseUrl"] = "http://localhost:5002/",
                ["ConnectionStrings:Payment"] = "Host=127.0.0.1;Port=5444;Database=smartpark_payment;Username=smartpark_payment;Password=smartpark_dev_password"
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

        // 3. Validators
        Assert.NotNull(provider.GetService<IValidator<CalculateParkingFeeQuery>>());
        Assert.NotNull(provider.GetService<IValidator<InitiatePaymentCheckoutCommand>>());
        Assert.NotNull(provider.GetService<IValidator<ProcessPayOsWebhookCommand>>());
        Assert.NotNull(provider.GetService<IValidator<GetPaymentStatusQuery>>());

        // 4. CQRS Handlers
        Assert.NotNull(provider.GetService<IRequestHandler<CalculateParkingFeeQuery, PricingCalculationResult>>());
        Assert.NotNull(provider.GetService<IRequestHandler<InitiatePaymentCheckoutCommand, PaymentCheckoutResponseDto>>());
        Assert.NotNull(provider.GetService<IRequestHandler<ProcessPayOsWebhookCommand, ProcessPayOsWebhookResult>>());
        Assert.NotNull(provider.GetService<IRequestHandler<GetPaymentStatusQuery, PaymentTransactionStatusDto>>());

        // 5. Hexagonal Ports & Adapters
        Assert.NotNull(provider.GetService<IParkingTariffPort>());
        Assert.NotNull(provider.GetService<IReservationPricingSnapshotPort>());
        Assert.NotNull(provider.GetService<IPaymentGatewayPort>());

        // 6. Persistence Repositories & UnitOfWork
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IPaymentOrderRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPaymentTransactionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IProviderWebhookRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IUnitOfWork>());
    }
}
