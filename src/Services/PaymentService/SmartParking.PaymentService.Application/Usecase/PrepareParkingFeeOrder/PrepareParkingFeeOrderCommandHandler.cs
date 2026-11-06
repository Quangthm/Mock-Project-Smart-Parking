using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.PrepareParkingFeeOrder;

public sealed class PrepareParkingFeeOrderCommandHandler : IRequestHandler<PrepareParkingFeeOrderCommand, PrepareParkingFeeOrderResponseDto>
{
    private readonly IParkingTariffPort _tariffPort;
    private readonly IParkingPricingCalculator _pricingCalculator;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentActor? _currentActor;
    private readonly ILogger<PrepareParkingFeeOrderCommandHandler> _logger;

    public PrepareParkingFeeOrderCommandHandler(
        IParkingTariffPort tariffPort,
        IParkingPricingCalculator pricingCalculator,
        IPaymentOrderRepository orderRepository,
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        ILogger<PrepareParkingFeeOrderCommandHandler> logger,
        ICurrentActor? currentActor = null)
    {
        _tariffPort = tariffPort;
        _pricingCalculator = pricingCalculator;
        _orderRepository = orderRepository;
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentActor = currentActor;
    }

    public async Task<PrepareParkingFeeOrderResponseDto> Handle(PrepareParkingFeeOrderCommand request, CancellationToken cancellationToken)
    {
        // 0. Authorization: Enforce S2S or Operator/Admin
        if (_currentActor != null && !_currentActor.IsInternalService && _currentActor.Role != "OPERATOR" && _currentActor.Role != "ADMIN")
        {
            _logger.LogWarning("Unauthorized prepare order attempt from caller role {Role}.", _currentActor.Role);
            throw new PaymentDomainException(
                PaymentDomainException.Unauthorized,
                "Yêu cầu định danh dịch vụ nội bộ (X-Internal-Service-Key hoặc Mutual S2S JWT) hoặc quyền hạn quản trị viên.");
        }

        // 1. Validate Cutoff Time
        if (request.PricingCutoffAt < request.EntryTime)
        {
            _logger.LogWarning("Invalid cutoff time: pricingCutoffAt ({PricingCutoffAt}) is earlier than entryTime ({EntryTime}).",
                request.PricingCutoffAt, request.EntryTime);

            throw new PaymentDomainException(
                PaymentDomainException.InvalidCutoffTime,
                "Thời điểm chốt cước pricingCutoffAt không thể nhỏ hơn thời điểm xe vào bãi entryTime.",
                nameof(request.PricingCutoffAt));
        }

        // 2. Check Idempotency by key
        var existingOrder = await _orderRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existingOrder != null)
        {
            if (existingOrder.TenantId != request.TenantId ||
                existingOrder.SiteId != request.SiteId ||
                existingOrder.ReferenceId != request.SessionId)
            {
                _logger.LogWarning("Idempotency conflict for key {Key}: payload mismatch with existing order {OrderId}.",
                    request.IdempotencyKey, existingOrder.Id);

                throw new PaymentDomainException(
                    PaymentDomainException.IdempotencyPayloadMismatch,
                    "Cùng idempotencyKey nhưng nội dung request khác nhau.",
                    nameof(request.IdempotencyKey));
            }

            var existingInvoice = await _invoiceRepository.GetByPaymentOrderIdAsync(existingOrder.Id, cancellationToken);
            _logger.LogInformation("Replaying existing order {OrderId} for idempotency key {Key}.", existingOrder.Id, request.IdempotencyKey);
            return new PrepareParkingFeeOrderResponseDto(
                OrderId: existingOrder.Id,
                OrderCode: existingOrder.OrderCode,
                SessionId: existingOrder.ReferenceId,
                Currency: existingInvoice?.PricingSnapshot.Currency ?? "VND",
                BaseParkingFee: existingInvoice?.BaseParkingFee ?? existingOrder.Amount,
                PenaltyFee: existingInvoice?.OverstayPenaltyFee ?? (request.PenaltyAmount ?? 0m),
                FinalAmount: existingOrder.FinalAmount,
                Status: existingOrder.Status.ToString(),
                TariffId: existingInvoice?.PricingSnapshot.TariffId ?? Guid.Empty,
                TariffVersion: existingInvoice?.PricingSnapshot.TariffVersion ?? 1,
                TotalParkedMinutes: existingInvoice?.TotalParkedMinutes ?? 0,
                ExpiresAt: existingOrder.ExpiresAt,
                CreatedAt: existingOrder.CreatedAt,
                DepositDeducted: existingOrder.DepositDeducted,
                SurplusRefund: existingInvoice?.PricingSnapshot.SurplusRefund ?? 0m
            );
        }

        // 3. Retrieve Tariff Policy
        if (!Enum.TryParse<PricingVehicleType>(request.VehicleType, ignoreCase: true, out var vehicleType))
        {
            throw new PaymentDomainException("UNSUPPORTED_VEHICLE_TYPE", $"Loại phương tiện '{request.VehicleType}' không được hỗ trợ.", nameof(request.VehicleType));
        }

        var activePolicy = await _tariffPort.GetActivePolicyAsync(request.TenantId, request.SiteId, vehicleType, cancellationToken);
        if (activePolicy == null)
        {
            _logger.LogWarning("No active tariff found for SiteId: {SiteId}, VehicleType: {VehicleType}", request.SiteId, vehicleType);
            throw new PaymentDomainException(
                PaymentDomainException.TariffNotFound,
                $"Không tìm thấy biểu giá hiệu lực cho bãi xe '{request.SiteId}' và loại phương tiện '{request.VehicleType}'.",
                nameof(request.VehicleType));
        }

        // 4. Calculate Parking Fee
        var calcRequest = new PricingCalculationRequest(
            TenantId: request.TenantId,
            SiteId: request.SiteId,
            VehicleType: vehicleType,
            EntryTime: request.EntryTime,
            ExitTime: request.PricingCutoffAt,
            Policy: activePolicy,
            SessionId: request.SessionId
        );

        var calcResult = _pricingCalculator.Calculate(calcRequest);

        var baseParkingFee = (decimal)calcResult.FinalAmountVnd;
        var penaltyFee = request.PenaltyAmount ?? 0m;
        var grossTotal = baseParkingFee + penaltyFee;
        var deposit = Math.Max(0, request.DepositAmount ?? 0m);
        var depositDeducted = Math.Min(deposit, grossTotal);
        var finalAmount = Math.Max(0, grossTotal - depositDeducted);
        var surplusRefund = Math.Max(0, deposit - grossTotal);

        // 5. Generate Order and Invoice
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + Random.Shared.Next(100, 999).ToString();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = request.TenantId,
            SiteId = request.SiteId,
            AccountId = request.AccountId,
            OrderCode = orderCode,
            IdempotencyKey = request.IdempotencyKey,
            OrderType = OrderType.PARKING_FEE,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = request.SessionId,
            Amount = grossTotal,
            DiscountAmount = 0,
            DepositDeducted = depositDeducted,
            FinalAmount = finalAmount,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        var invoiceNumber = await _invoiceRepository.GenerateInvoiceNumberAsync(cancellationToken);
        var invoiceId = Guid.NewGuid();

        var lineItems = new List<InvoiceLineItem>();
        foreach (var seg in calcResult.Segments)
        {
            lineItems.Add(new InvoiceLineItem
            {
                ItemType = "PARKING_TIME_BLOCK",
                Description = $"Đỗ xe khung giờ {seg.BlockCode}",
                StartTime = seg.SegmentStart,
                EndTime = seg.SegmentEnd,
                DurationMinutes = (int)Math.Round(seg.OverlapMinutes),
                CoveredMinutes = 0,
                ChargeableMinutes = (int)Math.Round(seg.OverlapMinutes),
                UnitRate = seg.RatePerHourVnd,
                Unit = "HOURLY_BLOCK",
                Amount = seg.RawChargeVnd
            });
        }

        if (penaltyFee > 0)
        {
            lineItems.Add(new InvoiceLineItem
            {
                ItemType = "PENALTY_OVERSTAY",
                Description = request.PenaltyReason ?? "Phí phạt vi phạm",
                StartTime = null,
                EndTime = null,
                DurationMinutes = 0,
                CoveredMinutes = 0,
                ChargeableMinutes = 0,
                UnitRate = penaltyFee,
                Unit = "FIXED_PENALTY",
                Amount = penaltyFee
            });
        }

        if (depositDeducted > 0)
        {
            lineItems.Add(new InvoiceLineItem
            {
                ItemType = "DEPOSIT_DEDUCTION",
                Description = "Khấu trừ tiền cọc đặt trước",
                StartTime = null,
                EndTime = null,
                DurationMinutes = 0,
                CoveredMinutes = 0,
                ChargeableMinutes = 0,
                UnitRate = -depositDeducted,
                Unit = "DEPOSIT_CREDIT",
                Amount = -depositDeducted
            });
        }

        if (surplusRefund > 0)
        {
            lineItems.Add(new InvoiceLineItem
            {
                ItemType = "SURPLUS_REFUND",
                Description = "Tiền cọc dư cần hoàn trả",
                StartTime = null,
                EndTime = null,
                DurationMinutes = 0,
                CoveredMinutes = 0,
                ChargeableMinutes = 0,
                UnitRate = surplusRefund,
                Unit = "SURPLUS_REFUND",
                Amount = surplusRefund
            });
        }

        var snapshot = new InvoicePricingSnapshot
        {
            OrderCode = orderCode,
            AccountId = request.AccountId,
            CustomerType = request.AccountId.HasValue ? "REGISTERED_DRIVER" : "GUEST_DRIVER",
            DisplayName = request.AccountId.HasValue ? null : MaskPlate(request.PlateNumber),
            SiteName = activePolicy.TariffName,
            VehicleType = request.VehicleType,
            PlateNumber = request.PlateNumber,
            EntryTime = request.EntryTime,
            PricingCutoffAt = request.PricingCutoffAt,
            TotalDurationMinutes = (int)Math.Round(calcResult.TotalParkedMinutes),
            BillableDurationMinutes = (int)Math.Round(calcResult.BilledMinutes),
            TariffId = activePolicy.TariffId,
            TariffVersion = activePolicy.PolicyVersion,
            PolicyName = activePolicy.TariffName,
            FreeParkingGraceMinutes = activePolicy.FreeParkingGraceMinutes,
            SubtotalParkingFee = baseParkingFee,
            TotalPenaltyFee = penaltyFee,
            DiscountAmount = 0m,
            DepositAmount = deposit,
            DepositDeducted = depositDeducted,
            SurplusRefund = surplusRefund,
            FinalTotalAmount = finalAmount,
            Currency = calcResult.Currency ?? "VND",
            Status = "PENDING",
            IsSettled = false,
            LineItems = lineItems
        };

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = request.TenantId,
            PaymentOrderId = order.Id,
            SiteId = request.SiteId,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = request.SessionId,
            SessionId = request.SessionId,
            InvoiceNumber = invoiceNumber,
            TotalParkedMinutes = (int)Math.Round(calcResult.TotalParkedMinutes),
            BaseParkingFee = baseParkingFee,
            OverstayPenaltyFee = penaltyFee,
            FinalAmount = finalAmount,
            PricingSnapshot = snapshot,
            CreatedAt = now
        };

        await _orderRepository.AddAsync(order, cancellationToken);
        await _invoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully prepared parking fee order {OrderId} (code: {OrderCode}, invoice: {InvoiceNumber}) for Session {SessionId}.",
            order.Id, order.OrderCode, invoice.InvoiceNumber, request.SessionId);

        return new PrepareParkingFeeOrderResponseDto(
            OrderId: order.Id,
            OrderCode: order.OrderCode,
            SessionId: order.ReferenceId,
            Currency: snapshot.Currency,
            BaseParkingFee: baseParkingFee,
            PenaltyFee: penaltyFee,
            FinalAmount: finalAmount,
            Status: order.Status.ToString(),
            TariffId: snapshot.TariffId,
            TariffVersion: snapshot.TariffVersion,
            TotalParkedMinutes: invoice.TotalParkedMinutes,
            ExpiresAt: order.ExpiresAt,
            CreatedAt: order.CreatedAt,
            DepositDeducted: depositDeducted,
            SurplusRefund: surplusRefund
        );
    }

    private static string MaskPlate(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate) || plate.Length <= 4)
            return plate;

        var prefix = plate[..3];
        var suffix = plate[^2..];
        return $"{prefix}***{suffix}";
    }
}
