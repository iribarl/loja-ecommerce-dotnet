using Loja.Customers.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loja.Customers.Infrastructure;

/// <summary>
/// Creates the store's admin account from configuration ("Admin:Email", "Admin:Password"),
/// kept in user-secrets or environment variables, never in the repository.
/// </summary>
internal static partial class AdminAccount
{
    public static async Task EnsureAsync(
        UserManager<Customer> users,
        RoleManager<IdentityRole<Guid>> roles,
        IConfiguration configuration,
        TimeProvider clock,
        ILogger logger)
    {
        if (!await roles.RoleExistsAsync(CustomerRoles.Admin))
        {
            ThrowIfFailed(await roles.CreateAsync(new IdentityRole<Guid>(CustomerRoles.Admin) { Id = Guid.CreateVersion7() }));
        }

        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogNoAdminConfigured(logger);
            return;
        }

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            var now = clock.GetUtcNow();
            admin = new Customer
            {
                UserName = email,
                Email = email,
                FullName = "Administrador Loja",
                PrivacyPolicyAcceptedAt = now,
                CreatedAt = now,
            };
            ThrowIfFailed(await users.CreateAsync(admin, password));
        }

        if (!await users.IsInRoleAsync(admin, CustomerRoles.Admin))
        {
            ThrowIfFailed(await users.AddToRoleAsync(admin, CustomerRoles.Admin));
        }
    }

    // A failure here is a configuration mistake (e.g. a too-short password): stop the start-up.
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the admin account: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No admin account configured (Admin:Email / Admin:Password): the admin panel has no user.")]
    private static partial void LogNoAdminConfigured(ILogger logger);
}
