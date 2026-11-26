using FluentValidation;

namespace UserService.Application.Media;

public sealed class UploadImageValidator : AbstractValidator<UploadImageCommand>
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public const int VehicleMaximumBytes = 1024 * 1024;

    public static int MaximumFor(string purpose) => purpose == "vehicle" ? VehicleMaximumBytes : MaximumBytes;

    public UploadImageValidator()
    {
        RuleFor(x => x.Purpose).Must(p => p is "avatar" or "vehicle");
        RuleFor(x => x.Content).Must((command, bytes) => bytes is { Length: > 0 } && bytes.Length <= MaximumFor(command.Purpose))
            .WithErrorCode("MEDIA_INVALID_IMAGE").WithMessage("Invalid image size.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9._:-]+$");
        RuleFor(x => x).Must(x => x.Purpose == "avatar" ? x.VehicleId is null && x.DraftIntentId is null
            : (x.VehicleId is not null) != (x.DraftIntentId is not null))
            .WithMessage("Vehicle uploads require exactly one vehicle or draft intent; avatars have neither.");
        RuleFor(x => x.VehicleId).Must(id => id is null || id != Guid.Empty);
        RuleFor(x => x.DraftIntentId).Must(id => id is null || id != Guid.Empty);
    }
}
