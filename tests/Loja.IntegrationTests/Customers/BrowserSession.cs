using System.Net;
using System.Text.RegularExpressions;

namespace Loja.IntegrationTests.Customers;

/// <summary>
/// A browser-like client: keeps cookies, follows redirects and submits Blazor forms
/// with the antiforgery token taken from the page.
/// </summary>
internal sealed partial class BrowserSession(HttpClient client)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HttpClient Client { get; } = client;

    public async Task<Page> GetAsync(string url) => await Page.FromAsync(await Client.GetAsync(url, Ct));

    /// <summary>Loads <paramref name="url"/> and submits its form named <paramref name="formName"/>.</summary>
    public async Task<Page> SubmitAsync(string url, string formName, Dictionary<string, string> fields)
    {
        var form = await GetAsync(url);
        return await PostAsync(url, formName, form.AntiforgeryToken, fields);
    }

    public async Task<Page> PostAsync(string url, string? formName, string antiforgeryToken, Dictionary<string, string> fields)
    {
        var content = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = antiforgeryToken };
        if (formName is not null)
        {
            content["_handler"] = formName;
        }

        return await Page.FromAsync(await Client.PostAsync(url, new FormUrlEncodedContent(content), Ct));
    }

    /// <param name="app">Which app instance; the main one by default.</param>
    public static async Task<BrowserSession> RegisterAsync(
        PostgresFixture postgres,
        string name,
        string email,
        string password = "senha-segura-1",
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? app = null)
    {
        var session = new BrowserSession((app ?? postgres.Web).CreateClient());
        var page = await session.SubmitAsync("/conta/cadastro", "register", new()
        {
            ["Input.FullName"] = name,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
            ["Input.AcceptPrivacyPolicy"] = "true",
        });
        page.Html.ShouldContain($"Olá, {name.Split(' ')[0]}");
        return session;
    }

    public static string NewEmail() => $"cliente-{Guid.NewGuid():N}@exemplo.com";

    internal sealed partial record Page(HttpStatusCode Status, Uri Url, string Html)
    {
        public string AntiforgeryToken => Token().Match(Html).Groups["token"].Value;

        public static async Task<Page> FromAsync(HttpResponseMessage response) =>
            new(response.StatusCode,
                response.RequestMessage!.RequestUri!,
                WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)));

        [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"(?<token>[^\"]+)\"")]
        private static partial Regex Token();
    }
}
