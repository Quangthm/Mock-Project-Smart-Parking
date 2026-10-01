namespace UserService.Application.Common.Models.Exceptions
{
    /// <summary>
    /// Not Found Exception.
    /// </summary>
    /// <seealso cref="Exception" />
    /// <remarks>
    /// Initializes a new instance of the <see cref="NotFoundException" /> class.
    /// </remarks>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorField">The error field.</param>
    /// <param name="errorMessage">The error message.</param>
    public class NotFoundException(string? errorCode, string? errorField, string? errorMessage) : Exception
    {

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        /// <value>
        /// The error code.
        /// </value>
        public string? ErrorCode { get; set; } = errorCode;

        /// <summary>
        /// Gets or sets the error field.
        /// </summary>
        /// <value>
        /// The error field.
        /// </value>
        public string? ErrorField { get; set; } = errorField;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public string? ErrorMessage { get; set; } = errorMessage;
    }
}
