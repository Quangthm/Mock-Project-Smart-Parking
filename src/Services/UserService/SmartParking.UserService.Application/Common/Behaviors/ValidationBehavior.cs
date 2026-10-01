using FluentValidation;
using MediatR;

namespace UserService.Application.Common.Behaviors
{
    /// <summary>
    /// Validation Pipeline Behavior
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <seealso cref="MediatR.IPipelineBehavior&lt;TRequest, TResponse&gt;" />
    public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        /// <summary>
        /// The validators
        /// </summary>
        private readonly IEnumerable<IValidator<TRequest>> validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationBehavior{TRequest, TResponse}" /> class.
        /// </summary>
        /// <param name="validators">The validators.</param>
        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            this.validators = validators;
        }

        /// <summary>
        /// Pipeline handler. Perform any additional behavior and await the <paramref name="next" /> delegate as necessary
        /// </summary>
        /// <param name="request">Incoming request</param>
        /// <param name="next">Awaitable delegate for the next action in the pipeline. Eventually this delegate represents the handler.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// Awaitable task returning the <typeparamref name="TResponse" />
        /// </returns>
        /// <exception cref="FluentValidation.ValidationException"></exception>
        /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException"></exception>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!this.validators.Any())
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }

            var context = new ValidationContext<TRequest>(request);
            var errors = this.validators.Select(v => v.Validate(context))
                                        .SelectMany(e => e.Errors)
                                        .Where(e => e is not null).ToList();

            if (errors.Count != 0)
            {
                throw new ValidationException(errors);
            }

            return await next(cancellationToken).ConfigureAwait(false);
        }
    }
}
