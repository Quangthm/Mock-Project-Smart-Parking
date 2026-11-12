using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class CreatePaymentOrderCommandHandlerTests
{
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    private CreatePaymentOrderCommandHandler CreateHandler()
    {
        return new CreatePaymentOrderCommandHandler(_orderRepo, _uow);
    }

    [Fact]
    public async Task Handle_WithCustomExpiresAt_CreatesOrderWithCustomExpiry()
    {
        var handler = CreateHandler();
        var customExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
        var request = new CreatePaymentOrderRequestDto(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            ReferenceId: Guid.NewGuid(),
            ReferenceType: ReferenceType.PARKING_SESSION,
            Amount: 20000,
            ExpiresAt: customExpiry
        );

        var result = await handler.Handle(new CreatePaymentOrderCommand(request), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(customExpiry, result.ExpiresAt);
        Assert.Single(_orderRepo.Orders);
        Assert.Equal(customExpiry, _orderRepo.Orders[0].ExpiresAt);
    }

    [Fact]
    public async Task Handle_IdempotentReplay_SamePayload_ReturnsExistingOrder()
    {
        var handler = CreateHandler();
        var key = "idem-key-100";
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var refId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var request1 = new CreatePaymentOrderRequestDto(
            TenantId: tenantId,
            SiteId: siteId,
            ReferenceId: refId,
            ReferenceType: ReferenceType.PARKING_SESSION,
            Amount: 20000,
            AccountId: accountId,
            IdempotencyKey: key
        );

        var result1 = await handler.Handle(new CreatePaymentOrderCommand(request1), CancellationToken.None);
        Assert.Single(_orderRepo.Orders);

        var request2 = new CreatePaymentOrderRequestDto(
            TenantId: tenantId,
            SiteId: siteId,
            ReferenceId: refId,
            ReferenceType: ReferenceType.PARKING_SESSION,
            Amount: 20000,
            AccountId: accountId,
            IdempotencyKey: key
        );

        var result2 = await handler.Handle(new CreatePaymentOrderCommand(request2), CancellationToken.None);

        Assert.Equal(result1.OrderId, result2.OrderId);
        Assert.Single(_orderRepo.Orders);
    }

    [Fact]
    public async Task Handle_IdempotencyMismatch_DifferentPayload_ThrowsConflict()
    {
        var handler = CreateHandler();
        var key = "idem-key-200";
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var refId = Guid.NewGuid();

        var request1 = new CreatePaymentOrderRequestDto(
            TenantId: tenantId,
            SiteId: siteId,
            ReferenceId: refId,
            ReferenceType: ReferenceType.PARKING_SESSION,
            Amount: 20000,
            IdempotencyKey: key
        );

        await handler.Handle(new CreatePaymentOrderCommand(request1), CancellationToken.None);

        var request2 = new CreatePaymentOrderRequestDto(
            TenantId: tenantId,
            SiteId: siteId,
            ReferenceId: refId,
            ReferenceType: ReferenceType.PARKING_SESSION,
            Amount: 50000, // Different amount!
            IdempotencyKey: key
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new CreatePaymentOrderCommand(request2), CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task Handle_ReservationFee_PublicCreation_ThrowsForbidden()
    {
        var handler = CreateHandler();
        var request = new CreatePaymentOrderRequestDto(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            ReferenceId: Guid.NewGuid(),
            ReferenceType: ReferenceType.RESERVATION,
            Amount: 30000,
            OrderType: OrderType.RESERVATION_FEE
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new CreatePaymentOrderCommand(request), CancellationToken.None));

        Assert.Equal("FORBIDDEN_RESERVATION_DEPOSIT_CREATION", ex.Code);
    }
}
