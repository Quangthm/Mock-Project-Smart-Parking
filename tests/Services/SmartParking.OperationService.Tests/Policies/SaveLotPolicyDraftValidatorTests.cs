using FluentValidation.TestHelper;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Validators;

namespace SmartParking.OperationService.Tests.Policies;

public class SaveLotPolicyDraftValidatorTests
{
    private readonly SaveLotPolicyDraftCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoValidationErrors()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                PaymentHoldDurationMinutes = 15,
                ArrivalLateToleranceMinutes = 20,
                MinReservationDurationMinutes = 30,
                MaxReservationDurationHours = 12,
                ReservationProtectionWindowHours = 4,
                AllocationLeadTimeMinutes = 60,
                OccupancyValidityTimespanHours = 4,
                MaxAdvanceBookingDays = 7,
                InterBookingBufferMinutes = 10,
                ScheduledActivationAt = DateTimeOffset.UtcNow.AddDays(1)
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PaymentHoldDurationBelowMin_HasValidationError()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                PaymentHoldDurationMinutes = 2 // < 5
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.PaymentHoldDurationMinutes);
    }

    [Fact]
    public void Validate_PaymentHoldDurationAboveMax_HasValidationError()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                PaymentHoldDurationMinutes = 45 // > 30
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.PaymentHoldDurationMinutes);
    }

    [Fact]
    public void Validate_ArrivalLateToleranceOutOfBounds_HasValidationError()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                ArrivalLateToleranceMinutes = 10 // < 15
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.ArrivalLateToleranceMinutes);
    }

    [Fact]
    public void Validate_MinDurationExceedsMaxDuration_HasValidationError()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                MinReservationDurationMinutes = 180, // 3 hours
                MaxReservationDurationHours = 2      // 2 hours = 120 min
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_PastScheduledActivation_HasValidationError()
    {
        var command = new SaveLotPolicyDraftCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            Request: new SavePolicyDraftRequest
            {
                ScheduledActivationAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.ScheduledActivationAt);
    }
}
