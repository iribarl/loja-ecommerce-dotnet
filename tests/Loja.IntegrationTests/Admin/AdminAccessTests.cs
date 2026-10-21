using Loja.Customers.Contracts;
using Loja.Customers.Infrastructure;
using Loja.IntegrationTests.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Admin;

/// <summary>
/// Only accounts with the Admin role may open the panel.
/// </summary>
public sealed class AdminAccessTests(PostgresFixture postgres)
{
    private const string Password = "senha-segura-1";

    [Fact]
    public async Task Admin_OpensThePanel_AndSeesItInTheHeader()
    {
        var admin = await SignedInAdmin();

        var page = await admin.GetAsync("/admin");

        page.Url.AbsolutePath.ShouldBe("/admin");
        page.Html.ShouldContain("Painel administrativo");
        page.Html.ShouldContain("href=\"/admin\"");
    }

    [Fact]
    public async Task Customer_IsDenied()
    {
        var customer = await BrowserSession.RegisterAsync(postgres, "Cliente Comum", BrowserSession.NewEmail());

        var page = await customer.GetAsync("/admin");

        page.Url.AbsolutePath.ShouldBe(CustomersModule.AccessDeniedPath);
        page.Html.ShouldContain("Acesso negado");
        page.Html.ShouldNotContain("Painel administrativo");
    }

    [Fact]
    public async Task Visitor_IsAskedToSignIn()
    {
        var page = await new BrowserSession(postgres.Web.CreateClient()).GetAsync("/admin");

        page.Url.AbsolutePath.ShouldBe(CustomersModule.LoginPath);
        page.Url.Query.ShouldContain("returnUrl=%2Fadmin");
    }

    // Registers a customer, gives them the role, and signs in again: the role is read into the
    // cookie at sign-in, as for a real admin.
    private async Task<BrowserSession> SignedInAdmin()
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Admin Teste", email, Password);

        await using (var scope = postgres.Web.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(CustomerRoles.Admin))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(CustomerRoles.Admin));
            }

            (await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, CustomerRoles.Admin)).Succeeded.ShouldBeTrue();
        }

        var admin = new BrowserSession(postgres.Web.CreateClient());
        var signedIn = await admin.SubmitAsync(CustomersModule.LoginPath, "login", new()
        {
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
        });
        signedIn.Html.ShouldContain("Olá, Admin");
        return admin;
    }
}
