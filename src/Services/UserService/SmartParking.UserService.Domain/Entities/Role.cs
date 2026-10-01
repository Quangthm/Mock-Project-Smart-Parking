using System;
using System.Collections.Generic;
using System.Text;
using UserService.Domain.Base;

namespace SmartParking.UserService.Domain.Entities
{
    public class Role : BaseEntity
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = [];
    }
}
