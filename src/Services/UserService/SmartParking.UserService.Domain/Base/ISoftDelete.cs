namespace UserService.Domain.Base
{
    /// <summary>
    /// Interface soft delete for base entity.
    /// </summary>
    public interface ISoftDelete
    {
        /// <summary>
        /// Gets or sets a value indicating whether this instance is deleted.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is deleted; otherwise, <c>false</c>.
        /// </value>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the deleted on.
        /// </summary>
        /// <value>
        /// The deleted on.
        /// </value>
        public DateTimeOffset DeletedOn { get; set; }
    }
}
