using System;
using System.Collections.Generic;
using System.Text;
using UserService.Domain.Base;

namespace SmartParking.UserService.Domain.Entities
{
    public class UserRole : BaseEntity
    {
        public Guid UserId { get; set; }

        public string RoleCode { get; set; } = string.Empty;

        public Guid? TenantId { get; set; }

        public Guid? SiteId { get; set; }

        public User User { get; set; } = null!;

        public Role Role { get; set; } = null!;
    }
}
