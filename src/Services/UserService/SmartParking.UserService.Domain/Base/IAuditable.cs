namespace UserService.Domain.Base
{
    /// <summary>
    /// Interface audit able for base entity.
    /// </summary>
    public interface IAuditable
    {
        /// <summary>
        /// Gets or sets the create on.
        /// </summary>
        /// <value>
        /// The create on.
        /// </value>
        public DateTimeOffset CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the created by.
        /// </summary>
        /// <value>
        /// The created by.
        /// </value>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the modified on.
        /// </summary>
        /// <value>
        /// The modified on.
        /// </value>
        public DateTimeOffset ModifiedOn { get; set; }

        /// <summary>
        /// Gets or sets the modified by.
        /// </summary>
        /// <value>
        /// The modified by.
        /// </value>
        public string? ModifiedBy { get; set; }
    }
}
