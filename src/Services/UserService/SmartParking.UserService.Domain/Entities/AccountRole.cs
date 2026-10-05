using System;

namespace SmartParking.UserService.Domain.Entities
{
    public class AccountRole
    {
        public Guid AccountId { get; set; }
        
        public string RoleCode { get; set; } = string.Empty;

        public Account Account { get; set; } = null!;
        
        public Role Role { get; set; } = null!;
    }
}
