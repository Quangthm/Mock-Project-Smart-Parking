using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public class EnsureReservationDepositOrderCommandHandlerTests
{
    private static ReservationQuote CreateSampleQuote(Guid quoteId, Guid accountId, decimal depositAmount = 50000m)
    {
        return new ReservationQuote
        {
            Id = quoteId,
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = accountId,
            VehicleId = Guid.NewGuid(),
            RequestedSlotId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddHours(1),
            EndTime = DateTimeOffset.UtcNow.AddHours(3),
            BookedAmount = depositAmount,
            DepositAmount = depositAmount,
            PrepaymentMode = PrepaymentMode.FULL,
            Currency = "VND",
            TariffId = Guid.NewGuid(),
            TermsVersion = "1",
            CoverageVersion = "STANDARD_v1",
            ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10),
            Status = ReservationQuoteStatus.ISSUED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task Handle_ValidRequest_AcceptsQuoteAndCreatesOrder()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var quoteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var quote = CreateSampleQuote(quoteId, accountId, 60000m);
        await quoteRepo.AddAsync(quote);

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var opId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var requestDto = new EnsureReservationDepositOrderRequestDto(
            OperationId: opId,
            ReservationId: resId,
            AccountId: accountId,
            TenantId: quote.TenantId,
            SiteId: quote.SiteId,
            QuoteId: quoteId,
            TermsVersion: "1",
            Amount: 60000m,
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: expiresAt
        );

        // Act
        var result = await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CREATED", result.State);
        Assert.NotNull(result.OrderId);

        // Verify quote state transition
        var updatedQuote = await quoteRepo.GetByIdAsync(quoteId);
        Assert.NotNull(updatedQuote);
        Assert.Equal(ReservationQuoteStatus.ACCEPTED, updatedQuote.Status);

        // Verify order created in DB
        Assert.Single(orderRepo.Orders);
        var createdOrder = orderRepo.Orders[0];
        Assert.Equal(result.OrderId, createdOrder.Id);
        Assert.Equal(OrderType.RESERVATION_FEE, createdOrder.OrderType);
        Assert.Equal(ReferenceType.RESERVATION, createdOrder.ReferenceType);
        Assert.Equal(resId, createdOrder.ReferenceId);
        Assert.Equal(quoteId, createdOrder.QuoteId);
        Assert.Equal("1", createdOrder.TermsVersion);
        Assert.Equal(60000m, createdOrder.Amount);
        Assert.Equal(PaymentOrderStatus.PENDING, createdOrder.Status);
        Assert.Equal(opId.ToString(), createdOrder.IdempotencyKey);
        Assert.Equal(expiresAt, createdOrder.ExpiresAt);
        Assert.Equal(quoteId, result.AcceptedRequest.QuoteId);
        Assert.Equal("1", result.AcceptedRequest.TermsVersion);
        Assert.True(uow.SaveCount > 0);
    }

    [Fact]
    public async Task Handle_ArrivesAfterExpiresAt_ReturnsClosedNoOrderAndPersistsTombstone()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var opId = Guid.NewGuid();
        var requestDto = new EnsureReservationDepositOrderRequestDto(
            OperationId: opId,
            ReservationId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            QuoteId: Guid.NewGuid(),
            TermsVersion: "1",
            Amount: 50000m,
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1) // H expired in past
        );

        // Act
        var result = await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.Equal("CLOSED_NO_ORDER", result.State);
        Assert.Null(result.OrderId);

        // Verify tombstone order in DB
        Assert.Single(orderRepo.Orders);
        var tombstone = orderRepo.Orders[0];
        Assert.Equal(PaymentOrderStatus.CANCELLED, tombstone.Status);
        Assert.Equal(opId.ToString(), tombstone.IdempotencyKey);
        Assert.Equal(requestDto.QuoteId, tombstone.QuoteId);
        Assert.Equal(requestDto.TermsVersion, tombstone.TermsVersion);
        Assert.True(uow.SaveCount > 0);

        // Act 2: Calling again with the same OperationId
        var replayResult = await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None);
        Assert.Equal("CLOSED_NO_ORDER", replayResult.State);
        Assert.Null(replayResult.OrderId);
        Assert.Single(orderRepo.Orders); // Still only 1 tombstone
    }

    [Fact]
    public async Task Handle_IdempotentReplayWithSamePayload_ReturnsExistingOrder()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var quoteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var quote = CreateSampleQuote(quoteId, accountId, 50000m);
        await quoteRepo.AddAsync(quote);

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var opId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var requestDto = new EnsureReservationDepositOrderRequestDto(
            OperationId: opId,
            ReservationId: resId,
            AccountId: accountId,
            TenantId: quote.TenantId,
            SiteId: quote.SiteId,
            QuoteId: quoteId,
            TermsVersion: "1",
            Amount: 50000m,
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        // Act 1: Initial call
        var firstResult = await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None);
        Assert.Equal("CREATED", firstResult.State);

        // Act 2: Replay call
        var secondResult = await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.Equal("CREATED", secondResult.State);
        Assert.Equal(firstResult.OrderId, secondResult.OrderId);
        Assert.Single(orderRepo.Orders); // No duplicate order created
    }

    [Fact]
    public async Task Handle_IdempotencyMismatch_ThrowsPaymentDomainException()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var quoteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var quote = CreateSampleQuote(quoteId, accountId, 50000m);
        await quoteRepo.AddAsync(quote);

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var opId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var requestDto1 = new EnsureReservationDepositOrderRequestDto(
            OperationId: opId,
            ReservationId: resId,
            AccountId: accountId,
            TenantId: quote.TenantId,
            SiteId: quote.SiteId,
            QuoteId: quoteId,
            TermsVersion: "1",
            Amount: 50000m,
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        await handler.Handle(new EnsureReservationDepositOrderCommand(requestDto1), CancellationToken.None);

        // Replay with different amount
        var requestDto2 = requestDto1 with { Amount = 80000m };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new EnsureReservationDepositOrderCommand(requestDto2), CancellationToken.None));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task Handle_ExpiredQuote_ThrowsInvalidOperationException()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var quoteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var quote = CreateSampleQuote(quoteId, accountId, 50000m);
        quote.ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-5); // Expired quote
        await quoteRepo.AddAsync(quote);

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var requestDto = new EnsureReservationDepositOrderRequestDto(
            OperationId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: quote.TenantId,
            SiteId: quote.SiteId,
            QuoteId: quoteId,
            TermsVersion: "1",
            Amount: 50000m,
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None));
        Assert.Equal("TERMS_STALE", ex.Code);
    }

    [Fact]
    public async Task Handle_AmountMismatchWithQuote_ThrowsPaymentDomainException()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();

        var quoteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var quote = CreateSampleQuote(quoteId, accountId, 50000m);
        await quoteRepo.AddAsync(quote);

        var handler = new EnsureReservationDepositOrderCommandHandler(
            orderRepo, quoteRepo, uow, NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

        var requestDto = new EnsureReservationDepositOrderRequestDto(
            OperationId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: quote.TenantId,
            SiteId: quote.SiteId,
            QuoteId: quoteId,
            TermsVersion: "1",
            Amount: 70000m, // Mismatch with 50,000 quote deposit
            Currency: "VND",
            PaymentMethod: "PAYOS",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new EnsureReservationDepositOrderCommand(requestDto), CancellationToken.None));
        Assert.Equal("QUOTE_AMOUNT_MISMATCH", ex.Code);
    }
}
