using System.Net;

namespace Loja.IntegrationTests.Customers;

/// <summary>
/// Sign up, sign in, sign out and protected pages, as a browser uses them.
/// </summary>
public sealed class AccountPagesTests(PostgresFixture postgres)
{
    private BrowserSession NewBrowser() => new(postgres.Web.CreateClient());

    [Fact]
    public async Task MyAccount_WhenAnonymous_RedirectsToLoginAndBack()
    {
        var page = await NewBrowser().GetAsync("/conta");

        page.Url.AbsolutePath.ShouldBe("/conta/entrar");
        page.Url.Query.ShouldContain("returnUrl=%2Fconta");
    }

    [Fact]
    public async Task Register_SignsInAndShowsTheCustomerData()
    {
        var email = BrowserSession.NewEmail();
        var session = await BrowserSession.RegisterAsync(postgres, "Maria da Silva", email);

        var account = await session.GetAsync("/conta");

        account.Status.ShouldBe(HttpStatusCode.OK);
        account.Html.ShouldContain("Maria da Silva");
        account.Html.ShouldContain(email);
    }

    [Fact]
    public async Task Register_WithAnEmailInUse_ShowsMessageInPortuguese()
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Ana Souza", email);

        var page = await NewBrowser().SubmitAsync("/conta/cadastro", "register", RegisterFields(email, accept: true));

        page.Html.ShouldContain("Já existe uma conta com este e-mail.");
        page.Html.ShouldNotContain("Olá, Ana");
    }

    [Fact]
    public async Task Register_WithoutAcceptingThePrivacyPolicy_IsRejected()
    {
        var page = await NewBrowser().SubmitAsync("/conta/cadastro", "register", RegisterFields(BrowserSession.NewEmail(), accept: false));

        page.Html.ShouldContain("Você precisa aceitar a Política de Privacidade.");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsAGenericMessage()
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "João Lima", email);

        var page = await NewBrowser().SubmitAsync("/conta/entrar", "login", LoginFields(email, "senha-errada-1"));

        page.Html.ShouldContain("E-mail ou senha incorretos.");
    }

    [Fact]
    public async Task Login_ReturnsToTheRequestedLocalPage()
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Carla Dias", email);

        var page = await NewBrowser().SubmitAsync("/conta/entrar?returnUrl=%2Fcarrinho", "login", LoginFields(email));

        page.Url.AbsolutePath.ShouldBe("/carrinho");
        page.Html.ShouldContain("Olá, Carla");
    }

    [Theory]
    [InlineData("https%3A%2F%2Floja-falsa.example")]
    [InlineData("%2F%2Floja-falsa.example")]
    public async Task Login_NeverRedirectsToAnotherSite(string returnUrl)
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Pedro Alves", email);

        var page = await NewBrowser().SubmitAsync($"/conta/entrar?returnUrl={returnUrl}", "login", LoginFields(email));

        page.Url.Host.ShouldBe("localhost");
        page.Url.AbsolutePath.ShouldBe("/");
    }

    [Fact]
    public async Task Logout_SignsOut()
    {
        var session = await BrowserSession.RegisterAsync(postgres, "Lucas Rocha", BrowserSession.NewEmail());
        var home = await session.GetAsync("/");

        var page = await session.PostAsync("/conta/sair", formName: null, home.AntiforgeryToken, []);

        page.Html.ShouldContain("Entrar");
        page.Html.ShouldNotContain("Olá, Lucas");
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryToken_IsRejected()
    {
        var session = await BrowserSession.RegisterAsync(postgres, "Bruna Melo", BrowserSession.NewEmail());

        var response = await session.Client.PostAsync("/conta/sair", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static Dictionary<string, string> RegisterFields(string email, bool accept) => new()
    {
        ["Input.FullName"] = "Ana Souza",
        ["Input.Email"] = email,
        ["Input.Password"] = "senha-segura-1",
        ["Input.ConfirmPassword"] = "senha-segura-1",
        ["Input.AcceptPrivacyPolicy"] = accept ? "true" : "false",
    };

    private static Dictionary<string, string> LoginFields(string email, string password = "senha-segura-1") => new()
    {
        ["Input.Email"] = email,
        ["Input.Password"] = password,
    };
}
