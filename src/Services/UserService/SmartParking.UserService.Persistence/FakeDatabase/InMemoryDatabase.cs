using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;


namespace UserService.Persistence.FakeDatabase;

public static class InMemoryDatabase
{
    public static List<User> Users { get; } =
    [
        new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),

            // SmartPark login account
            Email = "driver@gmail.com",

            FullName = "Demo Driver",

            // DEMO ONLY.
            // This is intentionally plain text for the first API demo.
            PasswordHash = "Password@123",

            Status = UserStatus.Active,
            UserRoles = [new UserRole { RoleCode = "Driver" }]
        }
    ];
}
