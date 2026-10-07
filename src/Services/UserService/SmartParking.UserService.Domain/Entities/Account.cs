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

        public User User { get; set; } = null!;
        
        public ICollection<AccountRole> AccountRoles { get; set; } = [];
    }
}
