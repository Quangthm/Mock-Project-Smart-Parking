using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Application.Usecase.SimulateDemoPayment;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public class SimulateDemoPayOsPaymentCommandHandlerTests
{
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeProviderWebhookRepository _webhookRepo = new();
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentActor _actor = new();

    private (SimulateDemoPayOsPaymentCommandHandler Handler, StubMediator Mediator) CreateSystem(
        string checksumKey = "demo-checksum-key")
    {
        var payOsOptions = Options.Create(new PayOsOptions
        {
            ClientId = "demo-client-id",
            ApiKey = "demo-api-key",
            ChecksumKey = checksumKey
        });
        var payOsAdapter = new PayOsAdapter(new HttpClient(), payOsOptions, NullLogger<PayOsAdapter>.Instance);
        var simulator = new PayOsWebhookSimulatorAdapter(payOsOptions);

        var webhookHandler = new ProcessPayOsWebhookCommandHandler(
            payOsAdapter,
            _webhookRepo,
            _orderRepo,
            _txRepo,
            _invoiceRepo,
            new ReviewedPayOsSettlement(
                _orderRepo,
                _txRepo,
                new FakeRefundRequestRepository(),
                _eventPublisher,
                _uow,
                NullLogger<ReviewedPayOsSettlement>.Instance),
            _eventPublisher,
            _uow,
            NullLogger<ProcessPayOsWebhookCommandHandler>.Instance);

        var mediator = new StubMediator(webhookHandler);

        var handler = new SimulateDemoPayOsPaymentCommandHandler(
            _orderRepo,
            _txRepo,
            _uow,
            simulator,
            _actor,
            mediator,
            NullLogger<SimulateDemoPayOsPaymentCommandHandler>.Instance);

        mediator.AttachSimulateHandler(handler);
        return (handler, mediator);
    }

    [Fact]
    public async Task Handle_ReservationOrderWithoutQr_CompletesFullWebhookAndOutboxFlow()
    {
        var driverId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        _actor.AccountId = driverId;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            PlateNumber = "51A-123.45",
            OrderCode = "1795664800123",
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = reservationId,
            Amount = 30000m,
            FinalAmount = 30000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        _orderRepo.Orders.Add(order);

        var (handler, _) = CreateSystem();
        var result = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);

        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(1795664800123L, result.OrderCode);
        Assert.Equal(30000L, result.Amount);
        Assert.Equal("PAYOS", result.PaymentMethod);
        Assert.Equal($"DEMO-LINK-{order.Id:N}", result.GatewayTransactionId);
        Assert.Equal("SUCCESS", result.Status);
        Assert.NotNull(result.PaidAt);
        Assert.True(result.IsAuthoritative);

        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Single(_txRepo.Transactions);
        Assert.Equal(PaymentStatus.SUCCESS, _txRepo.Transactions[0].Status);
        Assert.Single(_webhookRepo.Webhooks);
        Assert.Equal(WebhookProcessingState.PROCESSED, _webhookRepo.Webhooks[0].ProcessingState);

        var evt = Assert.IsType<PaymentSucceededV1>(Assert.Single(_eventPublisher.PublishedEvents));
        Assert.Equal("RESERVATION", evt.ReferenceType);
        Assert.Equal(reservationId, evt.ReferenceId);
        Assert.Equal(order.Id, evt.PaymentOrderId);
        Assert.Equal(30000m, evt.Amount);
        Assert.Equal("51A-123.45", evt.PlateNumber);
    }

    [Fact]
    public async Task Handle_ReviewedReservationOrderWithoutQr_SettlesThroughReviewedSettlement()
    {
        var driverId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        _actor.AccountId = driverId;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "1795664807777",
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = reservationId,
            ReviewedMethod = nameof(PaymentMethod.PAYOS),
            ReviewedCurrency = "VND",
            Amount = 35000m,
            FinalAmount = 35000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        _orderRepo.Orders.Add(order);

        var (handler, _) = CreateSystem();
        var result = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);

        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal($"DEMO-LINK-{order.Id:N}", result.GatewayTransactionId);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        var evt = Assert.IsType<PaymentSucceededV1>(Assert.Single(_eventPublisher.PublishedEvents));
        Assert.Equal("RESERVATION", evt.ReferenceType);
        Assert.Equal(reservationId, evt.ReferenceId);
        Assert.Equal(35000m, evt.Amount);
    }

    [Fact]
    public async Task Handle_ReservationOrderWithExistingPendingQrTransaction_ReusesPendingTransaction()
    {
        var driverId = Guid.NewGuid();
        _actor.AccountId = driverId;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "1795664800456",
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = Guid.NewGuid(),
            Amount = 25000m,
            FinalAmount = 25000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        var pendingTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            TransactionType = TransactionType.PAYMENT,
            PaymentMethod = PaymentMethod.PAYOS,
            GatewayTransactionId = "PAYOS-QR-LINK-777",
            Amount = 25000m,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        order.Transactions.Add(pendingTx);
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(pendingTx);

        var (handler, _) = CreateSystem();
        var result = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);

        Assert.Equal(pendingTx.Id, result.TransactionId);
        Assert.Equal("PAYOS-QR-LINK-777", result.GatewayTransactionId);
        Assert.Equal("SUCCESS", result.Status);
        Assert.Single(_txRepo.Transactions);
        Assert.Equal(PaymentStatus.SUCCESS, pendingTx.Status);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
    }

    [Fact]
    public async Task Handle_ParkingSessionCheckoutWithInvoice_SettlesInvoiceAndPublishesOutbox()
    {
        var sessionId = Guid.NewGuid();
        _actor.IsInternalService = true;
        _actor.Role = "INTERNAL_SERVICE";

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = null,
            PlateNumber = "59B1-678.90",
            OrderCode = "1795664800789",
            OrderType = OrderType.PARKING_FEE,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = sessionId,
            Amount = 40000m,
            FinalAmount = 40000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            SiteId = order.SiteId,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = sessionId,
            SessionId = sessionId,
            InvoiceNumber = "INV-20261127-0001",
            BaseParkingFee = 40000m,
            FinalAmount = 40000m,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = order.OrderCode,
                PlateNumber = "59B1-678.90",
                FinalTotalAmount = 40000m,
                Currency = "VND",
                Status = "PENDING",
                IsSettled = false
            }
        };
        _orderRepo.Orders.Add(order);
        _invoiceRepo.Invoices.Add(invoice);

        var (handler, _) = CreateSystem(checksumKey: "demo-session-checksum-key");
        var result = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);

        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal("PAID", invoice.PricingSnapshot.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Equal("PAYOS", invoice.PricingSnapshot.PaymentMethod);
        Assert.Equal(result.TransactionId.ToString(), invoice.PricingSnapshot.TransactionReference);

        var evt = Assert.IsType<PaymentSucceededV1>(Assert.Single(_eventPublisher.PublishedEvents));
        Assert.Equal("PARKING_SESSION", evt.ReferenceType);
        Assert.Equal(sessionId, evt.ReferenceId);
        Assert.Equal("59B1-678.90", evt.PlateNumber);
    }

    [Fact]
    public async Task Handle_CalledTwiceForSameOrder_IsIdempotentAndPublishesOnce()
    {
        var driverId = Guid.NewGuid();
        _actor.AccountId = driverId;
        _actor.UserId = driverId;

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "1795664800999",
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = Guid.NewGuid(),
            Amount = 20000m,
            FinalAmount = 20000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        _orderRepo.Orders.Add(order);

        var (handler, _) = CreateSystem();
        var first = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);
        var second = await handler.Handle(new SimulateDemoPayOsPaymentCommand(order.Id), CancellationToken.None);

        Assert.Equal(first.TransactionId, second.TransactionId);
        Assert.Equal("SUCCESS", second.Status);
        Assert.Single(_eventPublisher.PublishedEvents);
    }

    [Fact]
    public async Task Handle_DifferentDriverOrDriverOnWalkInOrder_ThrowsForbidden()
    {
        var ownerId = Guid.NewGuid();
        _actor.AccountId = Guid.NewGuid();
        _actor.UserId = _actor.AccountId;
        _actor.Role = "DRIVER";

        var ownedOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = ownerId,
            OrderCode = "1795664801111",
            FinalAmount = 20000m,
            Status = PaymentOrderStatus.PENDING
        };
        var walkInOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = null,
            OrderCode = "1795664802222",
            FinalAmount = 20000m,
            Status = PaymentOrderStatus.PENDING
        };
        _orderRepo.Orders.Add(ownedOrder);
        _orderRepo.Orders.Add(walkInOrder);

        var (handler, _) = CreateSystem();

        var ex1 = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(ownedOrder.Id), CancellationToken.None));
        Assert.Equal("FORBIDDEN_ORDER_ACCESS", ex1.Code);

        var ex2 = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(walkInOrder.Id), CancellationToken.None));
        Assert.Equal("FORBIDDEN_ORDER_ACCESS", ex2.Code);
    }

    [Fact]
    public async Task Handle_InvalidOrderStates_ThrowExpectedDomainExceptions()
    {
        var ownerId = Guid.NewGuid();
        _actor.AccountId = ownerId;
        _actor.UserId = ownerId;

        var cancelled = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = ownerId,
            OrderCode = "1795664803333",
            FinalAmount = 15000m,
            Status = PaymentOrderStatus.CANCELLED
        };
        var expired = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = ownerId,
            OrderCode = "1795664804444",
            FinalAmount = 15000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var zeroAmount = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = ownerId,
            OrderCode = "1795664805555",
            FinalAmount = 0m,
            Status = PaymentOrderStatus.PENDING
        };
        var badOrderCode = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = ownerId,
            OrderCode = "NOT-NUMERIC",
            FinalAmount = 15000m,
            Status = PaymentOrderStatus.PENDING
        };
        _orderRepo.Orders.AddRange([cancelled, expired, zeroAmount, badOrderCode]);

        var (handler, _) = CreateSystem();

        var notFound = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(PaymentDomainException.OrderNotFound, notFound.Code);

        var exCancelled = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(cancelled.Id), CancellationToken.None));
        Assert.Equal("ORDER_CANCELLED", exCancelled.Code);

        var exExpired = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(expired.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.OrderExpired, exExpired.Code);

        var exZero = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(zeroAmount.Id), CancellationToken.None));
        Assert.Equal("ZERO_AMOUNT_ORDER", exZero.Code);

        var exCode = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new SimulateDemoPayOsPaymentCommand(badOrderCode.Id), CancellationToken.None));
        Assert.Equal("INVALID_ORDER_CODE", exCode.Code);
    }

    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", null, true)]
    public async Task PaymentsController_DemoPay_RespectsEnableDemoBypassAndEnvironmentGuard(
        string environmentName,
        bool? enableDemoBypass,
        bool expectedAllowed)
    {
        var driverId = Guid.NewGuid();
        _actor.AccountId = driverId;
        _actor.UserId = driverId;
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "1795664806666",
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = Guid.NewGuid(),
            Amount = 25000m,
            FinalAmount = 25000m,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        _orderRepo.Orders.Add(order);

        var (_, mediator) = CreateSystem();
        var controller = new PaymentsController(
            mediator,
            NullLogger<PaymentsController>.Instance,
            new StubHostEnvironment { EnvironmentName = environmentName },
            Options.Create(new PayOsOptions
            {
                ChecksumKey = "demo-checksum-key",
                EnableDemoBypass = enableDemoBypass
            }));

        var actionResult = await controller.DemoPay(order.Id, CancellationToken.None);

        if (expectedAllowed)
        {
            Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        }
        else
        {
            Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(PaymentOrderStatus.PENDING, order.Status);
        }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SmartParking.PaymentService.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubMediator : IMediator
    {
        private readonly ProcessPayOsWebhookCommandHandler _webhookHandler;
        private SimulateDemoPayOsPaymentCommandHandler? _simulateHandler;

        public StubMediator(ProcessPayOsWebhookCommandHandler webhookHandler)
        {
            _webhookHandler = webhookHandler;
        }

        public void AttachSimulateHandler(SimulateDemoPayOsPaymentCommandHandler handler)
        {
            _simulateHandler = handler;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is ProcessPayOsWebhookCommand webhookCommand)
            {
                var result = await _webhookHandler.Handle(webhookCommand, cancellationToken);
                return (TResponse)(object)result;
            }

            if (request is SimulateDemoPayOsPaymentCommand simulateCommand && _simulateHandler != null)
            {
                var result = await _simulateHandler.Handle(simulateCommand, cancellationToken);
                return (TResponse)(object)result;
            }

            throw new NotSupportedException($"Unexpected MediatR request type: {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<object?> CreateStream(object request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new NotSupportedException();
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }
}
