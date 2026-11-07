using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.Checkout;

public sealed record InitiateCheckoutCommand(Guid OrderId, string IdempotencyKey)
    : IRequest<CheckoutPaymentCheckout>;

public sealed record ConfirmCashCheckoutCommand(Guid OrderId, string IdempotencyKey)
    : IRequest<CheckoutPaymentStatus>;

public sealed record DemoPayCheckoutCommand(Guid OrderId)
    : IRequest<CheckoutPaymentStatus>;

public sealed record GetCheckoutStatusQuery(Guid TransactionId)
    : IRequest<CheckoutPaymentStatus>;

public sealed class InitiateCheckoutCommandHandler(ICheckoutPaymentClient payment)
    : IRequestHandler<InitiateCheckoutCommand, CheckoutPaymentCheckout>
{
    public Task<CheckoutPaymentCheckout> Handle(InitiateCheckoutCommand request, CancellationToken ct) =>
        payment.InitiateAsync(request.OrderId, request.IdempotencyKey, ct);
}

public sealed class ConfirmCashCheckoutCommandHandler(ICheckoutPaymentClient payment)
    : IRequestHandler<ConfirmCashCheckoutCommand, CheckoutPaymentStatus>
{
    public Task<CheckoutPaymentStatus> Handle(ConfirmCashCheckoutCommand request, CancellationToken ct) =>
        payment.ConfirmCashAsync(request.OrderId, request.IdempotencyKey, ct);
}

public sealed class DemoPayCheckoutCommandHandler(ICheckoutPaymentClient payment)
    : IRequestHandler<DemoPayCheckoutCommand, CheckoutPaymentStatus>
{
    public Task<CheckoutPaymentStatus> Handle(DemoPayCheckoutCommand request, CancellationToken ct) =>
        payment.DemoPayAsync(request.OrderId, ct);
}

public sealed class GetCheckoutStatusQueryHandler(ICheckoutPaymentClient payment)
    : IRequestHandler<GetCheckoutStatusQuery, CheckoutPaymentStatus>
{
    public Task<CheckoutPaymentStatus> Handle(GetCheckoutStatusQuery request, CancellationToken ct) =>
        payment.GetStatusAsync(request.TransactionId, ct);
}
