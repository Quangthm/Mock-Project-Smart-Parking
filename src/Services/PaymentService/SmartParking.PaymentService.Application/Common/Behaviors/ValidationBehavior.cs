using FluentValidation;
using MediatR;

namespace SmartParking.PaymentService.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);
        var errors = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            if (result?.Errors is { Count: > 0 })
            {
                foreach (var error in result.Errors)
                {
                    if (error != null)
                    {
                        errors.Add(error);
                    }
                }
            }
        }

        if (errors.Count != 0)
        {
            throw new ValidationException(errors);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
