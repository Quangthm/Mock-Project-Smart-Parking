using SmartParking.UserService.Domain.Entities;

namespace SmartParking.UserService.Tests;

public sealed class AccountCurrentAuthorityTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void CurrentAuthorityDeniesInactiveOrWrongRole(bool user, bool account, bool role) =>
        Assert.False(new CurrentAccountAuthority(user, account, role, null).Allows("CREATE_RESERVATION"));

    [Fact]
    public void ExplicitPermissionsCanRevokeWithoutTokenOrSessionEcho()
    {
        Assert.True(new CurrentAccountAuthority(true, true, true, null).Allows("CREATE_RESERVATION"));
        Assert.False(new CurrentAccountAuthority(true, true, true, []).Allows("CREATE_RESERVATION"));
        var cancellationOnly = new CurrentAccountAuthority(true, true, true, ["reservation.cancel"]);
        Assert.True(cancellationOnly.Allows("CANCEL_OWN_RESERVATION"));
        Assert.False(cancellationOnly.Allows("CREATE_RESERVATION"));
        Assert.False(cancellationOnly.Allows("UNKNOWN_OPERATION"));
    }
}
