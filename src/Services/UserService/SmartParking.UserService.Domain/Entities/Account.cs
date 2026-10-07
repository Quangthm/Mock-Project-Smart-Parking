using System;
using System.Collections.Generic;
using UserService.Domain.Base;

namespace SmartParking.UserService.Domain.Entities
{
    public class Account : BaseEntity
    {
        public Guid UserId { get; set; }
        
        
        public Guid? TenantId { get; set; }
        
        public Guid? SiteId { get; set; }
        
        
        
        public string Status { get; set; } = "ACTIVE";
        // null uses the documented role matrix; an explicit empty set revokes all actions.
        public string[]? Permissions { get; set; }

        public User User { get; set; } = null!;
        
        public ICollection<AccountRole> AccountRoles { get; set; } = [];
    }
}
