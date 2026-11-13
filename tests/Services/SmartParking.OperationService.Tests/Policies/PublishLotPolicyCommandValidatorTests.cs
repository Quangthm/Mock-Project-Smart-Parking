using FluentValidation.TestHelper;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Validators;

namespace SmartParking.OperationService.Tests.Policies;

public class PublishLotPolicyCommandValidatorTests
{
    private readonly PublishLotPolicyCommandValidator _validator = new();

    [Fact]
    public void Validate_ImmediatePublish_Valid()
    {
        var command = new PublishLotPolicyCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Request: new PublishPolicyRequest
            {
                Mode = "IMMEDIATE"
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ScheduledPublish_WithFutureDate_Valid()
    {
        var command = new PublishLotPolicyCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Request: new PublishPolicyRequest
            {
                Mode = "SCHEDULED",
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(2)
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ScheduledPublish_MissingDate_HasValidationError()
    {
        var command = new PublishLotPolicyCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Request: new PublishPolicyRequest
            {
                Mode = "SCHEDULED",
                ScheduledAt = null
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.ScheduledAt);
    }

    [Fact]
    public void Validate_InvalidMode_HasValidationError()
    {
        var command = new PublishLotPolicyCommand(
            SiteId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Request: new PublishPolicyRequest
            {
                Mode = "UNKNOWN_MODE"
            }
        );

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.Mode);
    }
}
