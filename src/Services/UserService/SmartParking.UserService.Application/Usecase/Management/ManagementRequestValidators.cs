using FluentValidation;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Management;

// Reuse the established DTO constraints through the Application validation pipeline.
internal static class ManagementBodyValidation
{
    public static void Validate<TRequest>(object? body, ValidationContext<TRequest> context)
    {
        if (body is null) { context.AddFailure("A request body is required."); return; }
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(body,
            new System.ComponentModel.DataAnnotations.ValidationContext(body), results, true);
        foreach (var result in results)
            context.AddFailure(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? "Invalid value.");
    }
}

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator() => RuleFor(q => q.Filter).Custom((body, context) => ManagementBodyValidation.Validate(body, context));
}
public sealed class SetManagedUserStatusCommandValidator : AbstractValidator<SetManagedUserStatusCommand>
{
    public SetManagedUserStatusCommandValidator() => RuleFor(q => q.Body).Custom((body, context) => ManagementBodyValidation.Validate(body, context));
}
public sealed class CreateOperatorCommandValidator : AbstractValidator<CreateOperatorCommand>
{
    public CreateOperatorCommandValidator() => RuleFor(q => q.Body).Custom((body, context) => ManagementBodyValidation.Validate(body, context));
}
public sealed class ReviewOwnerApplicationCommandValidator : AbstractValidator<ReviewOwnerApplicationCommand>
{
    public ReviewOwnerApplicationCommandValidator() => RuleFor(q => q.Body).Custom((body, context) => ManagementBodyValidation.Validate(body, context));
}
public sealed class SaveOwnVehicleCommandValidator : AbstractValidator<SaveOwnVehicleCommand>
{
    public SaveOwnVehicleCommandValidator() => RuleFor(q => q.Body).Custom((body, context) => ManagementBodyValidation.Validate(body, context));
}
