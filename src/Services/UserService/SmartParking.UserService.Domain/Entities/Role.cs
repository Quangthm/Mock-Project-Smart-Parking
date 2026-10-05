using System.Collections.Generic;

namespace SmartParking.UserService.Domain.Entities
{
    public class Role
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<AccountRole> AccountRoles { get; set; } = [];
    }
}
