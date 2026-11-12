using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.CreateReservationQuote;

public sealed class CreateReservationQuoteCommandHandler
    : IRequestHandler<CreateReservationQuoteCommand, CommercialTermsResponseDto>
{
    private readonly ICurrentActor _currentActor;
    private readonly IParkingPricingCalculator _calculator;
    private readonly IParkingTariffPort _tariffPort;
    private readonly IReservationQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateReservationQuoteCommandHandler> _logger;

    public CreateReservationQuoteCommandHandler(
        ICurrentActor currentActor,
        IParkingPricingCalculator calculator,
        IParkingTariffPort tariffPort,
        IReservationQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateReservationQuoteCommandHandler> logger)
    {
        _currentActor = currentActor;
        _calculator = calculator;
        _tariffPort = tariffPort;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CommercialTermsResponseDto> Handle(
        CreateReservationQuoteCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        var accountId = _currentActor.AccountId ?? req.AccountId;
        if (!accountId.HasValue || accountId.Value == Guid.Empty)
        {
            throw new PaymentDomainException(PaymentDomainException.Unauthorized, "Authenticated user account is required to create a reservation quote.", "accountId");
        }

        _logger.LogInformation(
            "Creating reservation quote for AccountId: {AccountId}, SiteId: {SiteId}, SlotId: {SlotId}",
            accountId.Value, req.SiteId, req.RequestedSlotId);

        var vehicleType = PricingVehicleType.CAR;
        if (!string.IsNullOrWhiteSpace(req.VehicleType) &&
            Enum.TryParse<PricingVehicleType>(req.VehicleType, true, out var parsedType))
        {
            vehicleType = parsedType;
        }

        var policy = await _tariffPort.GetActivePolicyAsync(
            req.TenantId ?? Guid.Empty,
            req.SiteId,
            vehicleType,
            cancellationToken);

        if (policy == null)
        {
            _logger.LogWarning(
                "No active tariff found for SiteId: {SiteId}, VehicleType: {VehicleType}",
                req.SiteId, vehicleType);

            throw new PricingDomainException(
                "TARIFF_NOT_FOUND",
                $"No active tariff found for site '{req.SiteId}' and vehicle type '{vehicleType}'.",
                "siteId");
        }

        var calcRequest = new PricingCalculationRequest(
            TenantId: req.TenantId ?? Guid.Empty,
            SiteId: req.SiteId,
            VehicleType: vehicleType,
            EntryTime: req.Start,
            ExitTime: req.End,
            Policy: policy
        );

        var calcResult = _calculator.Calculate(calcRequest);
        decimal bookedAmount = calcResult.FinalAmountVnd;

        var prepaymentMode = PrepaymentMode.FULL;
        if (!string.IsNullOrWhiteSpace(req.PrepaymentMode) &&
            Enum.TryParse<PrepaymentMode>(req.PrepaymentMode, true, out var parsedMode))
        {
            prepaymentMode = parsedMode;
        }

        decimal depositAmount = prepaymentMode switch
        {
            PrepaymentMode.FULL => bookedAmount,
            PrepaymentMode.PERCENTAGE => Math.Truncate(bookedAmount * 0.5m),
            PrepaymentMode.FIXED => Math.Min(bookedAmount, 50000m),
            _ => bookedAmount
        };

        if (bookedAmount > 0 && depositAmount <= 0)
        {
            depositAmount = bookedAmount;
        }
        else if (bookedAmount == 0)
        {
            depositAmount = 0;
        }

        var now = DateTimeOffset.UtcNow;
        var validUntil = now.AddMinutes(10);

        var quote = new ReservationQuote
        {
            Id = Guid.NewGuid(),
            TenantId = req.TenantId ?? Guid.Empty,
            SiteId = req.SiteId,
            AccountId = accountId.Value,
            VehicleId = req.VehicleId,
            RequestedSlotId = req.RequestedSlotId,
            StartTime = req.Start,
            EndTime = req.End,
            BookedAmount = bookedAmount,
            DepositAmount = depositAmount,
            PrepaymentMode = prepaymentMode,
            Currency = "VND",
            TariffId = policy.TariffId,
            TermsVersion = policy.PolicyVersion.ToString(),
            CoverageVersion = req.CoverageVersion ?? "STANDARD_v1",
            ValidUntil = validUntil,
            Status = ReservationQuoteStatus.ISSUED,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _quoteRepository.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Issued ReservationQuote '{QuoteId}' with BookedAmount: {BookedAmount}, DepositAmount: {DepositAmount}, ValidUntil: {ValidUntil}",
            quote.Id, quote.BookedAmount, quote.DepositAmount, quote.ValidUntil);

        return new CommercialTermsResponseDto(
            QuoteId: quote.Id,
            Version: quote.TermsVersion,
            ValidUntil: quote.ValidUntil,
            Selection: new BookingSelectionDto(
                VehicleId: quote.VehicleId.Value,
                SiteId: quote.SiteId,
                RequestedSlotId: quote.RequestedSlotId.Value,
                Start: quote.StartTime,
                End: quote.EndTime,
                PaymentMethod: req.PaymentMethod
            ),
            AccountId: quote.AccountId,
            BookedAmount: quote.BookedAmount,
            DepositAmount: quote.DepositAmount,
            Currency: quote.Currency,
            PrepaymentMode: quote.PrepaymentMode.ToString(),
            CoverageVersion: quote.CoverageVersion ?? "STANDARD_v1",
            Disclosure: "Non-refundable deposit applies if cancelled within 15 minutes of reservation start time."
        );
    }
}
