using Loja.Customers.Contracts;
using Loja.Customers.Infrastructure;
using Loja.IntegrationTests.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Admin;

internal static class AdminSession
{
    private const string Password = "senha-segura-1";

    /// <summary>
    /// Registers a customer, gives them the Admin role and signs in again: the role is read into
    /// the cookie at sign-in, as for a real admin.
    /// </summary>
    public static async Task<BrowserSession> SignInAsync(PostgresFixture postgres, WebApplicationFactory<Program> app)
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Admin Teste", email, Password, app);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(CustomerRoles.Admin))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(CustomerRoles.Admin));
            }

            (await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, CustomerRoles.Admin)).Succeeded.ShouldBeTrue();
        }

        var admin = new BrowserSession(app.CreateClient());
        var signedIn = await admin.SubmitAsync(CustomersModule.LoginPath, "login", new()
        {
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
        });
        signedIn.Html.ShouldContain("Olá, Admin");
        return admin;
    }
}
