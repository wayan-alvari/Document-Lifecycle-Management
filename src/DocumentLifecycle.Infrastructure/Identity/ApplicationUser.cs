using Microsoft.AspNetCore.Identity;

namespace DocumentLifecycle.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public required string DisplayName { get; set; }
}
