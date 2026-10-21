using Loja.Customers.Infrastructure;
using Loja.IntegrationTests.Customers;

namespace Loja.IntegrationTests.Admin;

/// <summary>
/// Only accounts with the Admin role may open the panel.
/// </summary>
public sealed partial class AdminAccessTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Admin_OpensThePanel_AndSeesItInTheHeader()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.Web);

        var page = await admin.GetAsync("/admin");

        page.Url.AbsolutePath.ShouldBe("/admin");
        page.Html.ShouldContain("Painel administrativo");
        page.Html.ShouldContain("href=\"/admin\"");

        // The panel has its own layout and stylesheet, on top of the default theme's.
        page.Html.ShouldContain("Ver loja");
        AdminStylesheet().IsMatch(page.Html).ShouldBeTrue();
        AdminStylesheet().IsMatch((await admin.GetAsync("/")).Html).ShouldBeFalse();
    }

    [System.Text.RegularExpressions.GeneratedRegex("href=\"admin(\\.[a-z0-9]+)?\\.css\"")]
    private static partial System.Text.RegularExpressions.Regex AdminStylesheet();

    // Pages in subfolders of Pages/Admin are protected by the folder's _Imports.razor too.
    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/produtos")]
    [InlineData("/admin/produtos/novo")]
    [InlineData("/admin/pedidos")]
    public async Task Customer_IsDenied(string url)
    {
        var customer = await BrowserSession.RegisterAsync(postgres, "Cliente Comum", BrowserSession.NewEmail());

        var page = await customer.GetAsync(url);

        page.Url.AbsolutePath.ShouldBe(CustomersModule.AccessDeniedPath);
        page.Html.ShouldContain("Acesso negado");
    }

    [Fact]
    public async Task Visitor_IsAskedToSignIn()
    {
        var page = await new BrowserSession(postgres.Web.CreateClient()).GetAsync("/admin");

        page.Url.AbsolutePath.ShouldBe(CustomersModule.LoginPath);
        page.Url.Query.ShouldContain("returnUrl=%2Fadmin");
    }
}
