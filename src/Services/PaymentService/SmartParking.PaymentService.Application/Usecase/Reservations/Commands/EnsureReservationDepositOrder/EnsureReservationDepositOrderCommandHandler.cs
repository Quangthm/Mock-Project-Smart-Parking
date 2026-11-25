using SmartParking.Contracts.ReservationPayments;
using MediatR;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed class EnsureReservationDepositOrderCommandHandler(IPaymentOrderRepository orders,
    IReservationQuoteRepository quotes, IUnitOfWork work, IReservationFinancialNotifications notifications)
    : IRequestHandler<EnsureReservationDepositOrderCommand>
{
    public Task Handle(EnsureReservationDepositOrderCommand command, CancellationToken ct)
    {
        var request = command.Request;
        return work.WithLocksAsync([request.ReservationId, request.QuoteId], async () =>
        {
            var quote = await quotes.GetByIdAsync(request.QuoteId, ct);
            if (quote?.ReviewedEvidence is null || quote.AccountId != request.AccountId)
                throw new PaymentDomainException("QUOTE_NOT_FOUND", "Reviewed quote was not found.");
            var proof = ReviewedQuoteProof.From(quote);
            var binding = proof.Binding;
            var zero = proof.DepositAmount == 0;
            if (request.ReservationId == Guid.Empty || request.AccountId != binding.AccountId ||
                request.TenantId != binding.TenantId || request.SiteId != binding.SiteId ||
                request.RequestedSlotId != binding.RequestedSlotId || request.CanonicalPlate != binding.CanonicalPlate ||
                request.Start != binding.Start || request.End != binding.End || request.PaymentMethod != binding.PaymentMethod ||
                request.IsCovered != binding.Coverage.IsCovered || request.CoverageVersion != binding.Coverage.Version ||
                request.DepositAmount != proof.DepositAmount || request.Currency != proof.Currency ||
                zero != (request.HoldExpiresAt is null) || request.HoldExpiresAt is { } h &&
                (h == default || h.UtcTicks % 10 != 0 || h > binding.Start))
                throw Conflict();
            var key = request.ReservationId.ToString();
            var existing = await orders.GetByIdempotencyKeyAsync(key, ct);
            if (existing is not null)
            {
                if (existing.ReferenceType != ReferenceType.RESERVATION || existing.OrderType != OrderType.RESERVATION_FEE ||
                    existing.ReferenceId != request.ReservationId || existing.AccountId != request.AccountId ||
                    existing.SiteId != request.SiteId || existing.TenantId != request.TenantId ||
                    existing.QuoteId != request.QuoteId || existing.ReviewedCurrency != request.Currency ||
                    existing.ReviewedMethod != request.PaymentMethod || existing.FinalAmount != request.DepositAmount ||
                    existing.ExpiresAt != request.HoldExpiresAt || existing.PlateNumber != binding.CanonicalPlate ||
                    zero && (existing.Status != PaymentOrderStatus.PAID || existing.Invoice is not { } savedReceipt ||
                        savedReceipt.Id == Guid.Empty || savedReceipt.PaymentOrderId != existing.Id || savedReceipt.ReferenceId != request.ReservationId ||
                        savedReceipt.ReferenceType != ReferenceType.RESERVATION || savedReceipt.TenantId != binding.TenantId ||
                        savedReceipt.SiteId != binding.SiteId || savedReceipt.FinalAmount != 0 || !savedReceipt.PricingSnapshot.IsSettled ||
                        savedReceipt.PricingSnapshot.Currency != proof.Currency))
                    throw Conflict();
                return existing.Id;
            }
            if (quote.Status != ReservationQuoteStatus.ISSUED || await orders.HasReviewedReservationAsync(request.ReservationId, ct))
                throw Conflict();
            var now = await work.DatabaseNowAsync(ct);
            var closed = !zero && now >= request.HoldExpiresAt;
            if (closed) quote.MarkExpired();
            else quote.MarkAccepted();
            var order = new PaymentOrder
            {
                Id = Guid.NewGuid(), OrderCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999)
                    .ToString() + now.ToUnixTimeMilliseconds().ToString()[^9..],
                IdempotencyKey = key, TenantId = binding.TenantId, SiteId = binding.SiteId, AccountId = binding.AccountId,
                OrderType = OrderType.RESERVATION_FEE, ReferenceType = ReferenceType.RESERVATION,
                ReferenceId = request.ReservationId, PlateNumber = binding.CanonicalPlate,
                QuoteId = proof.QuoteId, Amount = proof.DepositAmount,
                FinalAmount = proof.DepositAmount, ReviewedMethod = binding.PaymentMethod, ReviewedCurrency = proof.Currency,
                ExpiresAt = request.HoldExpiresAt?.ToUniversalTime(), CreatedAt = now, UpdatedAt = now,
                IsProvisioningFence = closed, Status = closed ? null : PaymentOrderStatus.PENDING
            };
            if (zero)
            {
                order.MarkPaid(now);
                order.Invoice = new Invoice
                {
                    Id = Guid.NewGuid(), PaymentOrderId = order.Id, ReferenceId = order.ReferenceId,
                    ReferenceType = ReferenceType.RESERVATION, SiteId = order.SiteId, TenantId = order.TenantId,
                    InvoiceNumber = $"INV-R-{order.Id:N}", BaseParkingFee = proof.BookedAmount,
                    PricingSnapshot = new InvoicePricingSnapshot
                    {
                        AccountId = order.AccountId, OrderCode = order.OrderCode, CustomerType = "REGISTERED_DRIVER",
                        PlateNumber = binding.CanonicalPlate, EntryTime = binding.Start, PricingCutoffAt = binding.End,
                        TariffId = proof.Breakdown.AppliedTariffId, TariffVersion = proof.Breakdown.AppliedPolicyVersion,
                        PolicyName = proof.Breakdown.AppliedTariffName, Currency = proof.Currency,
                        SubtotalParkingFee = proof.BookedAmount, DiscountAmount = proof.CoveredAmount,
                        Status = "PAID", IsSettled = true
                    }
                };
            }
            await orders.AddAsync(order, ct);
            if (order.Invoice is { } receipt)
                await notifications.SendAsync(new ReservationZeroReceiptIssuedV1(
                    request.ReservationId, binding.AccountId, binding.TenantId, binding.SiteId,
                    proof.QuoteId, order.Id, receipt.Id, proof.DepositAmount, proof.Currency), ct);
            else
                await notifications.SendAsync(new ReservationCheckoutChangedV1(
                    binding.AccountId, request.ReservationId), ct);
            await work.SaveChangesAsync(ct);
            return order.Id;
        }, ct);
    }

    private static PaymentDomainException Conflict() => new("QUOTE_BINDING_CONFLICT", "Reservation order binding is inconsistent.");
}
