using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace DocumentLifecycle.Infrastructure.Identity;

public sealed class DemoIdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in ApplicationRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            }
        }

        foreach (var account in DemoAccounts.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(account.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserName = account.Email,
                    NormalizedUserName = account.Email.ToUpperInvariant(),
                    Email = account.Email,
                    NormalizedEmail = account.Email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    DisplayName = account.Role,
                    LockoutEnabled = true,
                };

                EnsureSucceeded(
                    await userManager.CreateAsync(user, account.Password),
                    $"create demo user for role '{account.Role}'");
            }

            if (!await userManager.IsInRoleAsync(user, account.Role))
            {
                EnsureSucceeded(
                    await userManager.AddToRoleAsync(user, account.Role),
                    $"assign demo role '{account.Role}'");
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var safeCodes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Identity initialization could not {operation}: {safeCodes}");
    }
}
