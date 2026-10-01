namespace UserService.Application.Common.Models.Exceptions
{
    /// <summary>
    /// Error message.
    /// </summary>
    public class ErrorMessage
    {
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>
        /// The message.
        /// </value>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        /// <value>
        /// The error details.
        /// </value>
        public List<ErrorDetail> ErrorDetails { get; set; } = [];
    }

    /// <summary>
    /// 
    /// </summary>
    public class ErrorDetail
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        /// <value>
        /// The error code.
        /// </value>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the error field.
        /// </summary>
        /// <value>
        /// The error field.
        /// </value>
        public string? ErrorField { get; set; }
    }
}
