using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
                ["Services:OperationService:BaseUrl"] = "http://localhost:5055/"
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

        // 5. Hexagonal Ports & Persistence Adapters
        var tariffPort = provider.GetService<IParkingTariffPort>();
        Assert.NotNull(tariffPort);
        Assert.IsType<TariffPersistenceAdapter>(tariffPort);

        // 6. Persistence Repositories & UnitOfWork
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IPaymentOrderRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPaymentTransactionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IProviderWebhookRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IUnitOfWork>());
    }
}
