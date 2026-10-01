namespace UserService.Domain.Base
{
    /// <summary>
    /// Base Entity.
    /// </summary>
    /// <seealso cref="UserService.Domain.Base.IAuditable" />
    /// <seealso cref="UserService.Domain.Base.ISoftDelete" />
    public class BaseEntity : IAuditable, ISoftDelete
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public Guid? Id { get; set; }

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
        public DateTimeOffset DeletedOn { get ; set ; }
    }
}
