using DocumentLifecycle.Application.Security;

namespace DocumentLifecycle.UnitTests.Security;

public sealed class DemoAccountTests
{
    [Fact]
    public void DemoAccountsCoverEveryRoleWithUniqueEmailAddresses()
    {
        Assert.Equal(ApplicationRoles.All.Count, DemoAccounts.All.Count);
        Assert.Equal(
            DemoAccounts.All.Count,
            DemoAccounts.All.Select(account => account.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            ApplicationRoles.All.Order(StringComparer.Ordinal).ToArray(),
            DemoAccounts.All.Select(account => account.Role).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void SharedPasswordMeetsConfiguredIdentityRequirements()
    {
        var password = DemoAccounts.SharedPassword;

        Assert.True(password.Length >= 12);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, character => !char.IsLetterOrDigit(character));
    }
}
