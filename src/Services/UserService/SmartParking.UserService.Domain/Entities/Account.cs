using System;
using UserService.Domain.Base;

namespace SmartParking.UserService.Domain.Entities
{
    public class Account : BaseEntity
    {
        public Guid UserId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        
        
        public Guid? TenantId { get; set; }
        
        public Guid? SiteId { get; set; }
        
        
        
        public string Status { get; set; } = "ACTIVE";
        // null uses the documented role matrix; an explicit empty set revokes all actions.
        public string[]? Permissions { get; set; }

        public User User { get; set; } = null!;
        
        public Role Role { get; set; } = null!;
    }
}
