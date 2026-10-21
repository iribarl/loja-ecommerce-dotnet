using Loja.Catalog.Infrastructure.Persistence;
using Loja.IntegrationTests.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Admin;

/// <summary>
/// Managing products in the panel, on the app instance whose catalog tests may change
/// (the catalog listing tests run on the other one).
/// </summary>
public sealed class AdminProductsPagesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateProduct_AddVariant_Publish_ShowsInTheStore()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var name = UniqueName();

        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(name));
        created.Html.ShouldContain("Produto salvo.");
        created.Html.ShouldContain("Rascunho");
        var productUrl = created.Url.AbsolutePath;
        var slug = Slug(name);

        // Not in the store while a draft.
        (await admin.GetAsync($"/produtos/{slug}")).Status.ShouldBe(System.Net.HttpStatusCode.NotFound);

        var withVariant = await admin.SubmitAsync($"{productUrl}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = $"ADM-{Guid.NewGuid():N}"[..20],
            ["Input.Attributes"] = "Tamanho: M\nCor: Verde",
            ["Input.Price"] = "79.90",
            ["Input.Stock"] = "0",
        });
        withVariant.Url.AbsolutePath.ShouldBe(productUrl);
        withVariant.Html.ShouldContain("Cor: Verde, Tamanho: M");

        var published = await admin.PostAsync(productUrl, "publish", withVariant.AntiforgeryToken, []);
        published.Html.ShouldContain("Publicado");

        var storePage = await admin.GetAsync($"/produtos/{slug}");
        storePage.Html.ShouldContain(name);
        storePage.Html.ShouldContain("79,90");
    }

    [Fact]
    public async Task Rename_ShowsInTheStoreAtOnce_AndKeepsTheUrl()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var (productUrl, slug) = await PublishedProduct(admin, UniqueName());
        (await admin.GetAsync($"/produtos/{slug}")).Status.ShouldBe(System.Net.HttpStatusCode.OK); // now cached

        var newName = UniqueName();
        var fields = await Product(newName);
        await admin.SubmitAsync(productUrl, "product", fields);

        // The cache was dropped: the store shows the new name under the old URL.
        (await admin.GetAsync($"/produtos/{slug}")).Html.ShouldContain(newName);
    }

    [Fact]
    public async Task List_FindsByNameOrSku_AndFiltersByStatus()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var name = UniqueName();
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(name));
        var sku = $"BUSCA-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await admin.SubmitAsync($"{created.Url.AbsolutePath}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = sku,
            ["Input.Attributes"] = "Tamanho: P",
            ["Input.Price"] = "10",
            ["Input.Stock"] = "0",
        });

        (await admin.GetAsync($"/admin/produtos?q={Uri.EscapeDataString(name)}")).Html.ShouldContain(name);
        (await admin.GetAsync($"/admin/produtos?q={sku[..12]}")).Html.ShouldContain(name);
        (await admin.GetAsync($"/admin/produtos?q={Uri.EscapeDataString(name)}&status=rascunhos")).Html.ShouldContain(name);
        (await admin.GetAsync($"/admin/produtos?q={Uri.EscapeDataString(name)}&status=publicados")).Html.ShouldContain("Nenhum produto encontrado.");
    }

    [Fact]
    public async Task Mistakes_AreExplained()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);

        var noCategory = await admin.SubmitAsync("/admin/produtos/novo", "product", new() { ["Input.Name"] = UniqueName() });
        noCategory.Html.ShouldContain("Escolha a categoria.");

        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(UniqueName()));
        var productUrl = created.Url.AbsolutePath;

        var badAttributes = await admin.SubmitAsync($"{productUrl}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = $"ERR-{Guid.NewGuid():N}"[..20],
            ["Input.Attributes"] = "Tamanho M",
            ["Input.Price"] = "10",
            ["Input.Stock"] = "1",
        });
        badAttributes.Html.ShouldContain("Os atributos da variante não podem ter nome ou valor vazio.");

        var publishEmpty = await admin.PostAsync(productUrl, "publish", created.AntiforgeryToken, []);
        publishEmpty.Html.ShouldContain("Um produto precisa de ao menos uma variante para ser publicado.");
    }

    private async Task<(string ProductUrl, string Slug)> PublishedProduct(BrowserSession admin, string name)
    {
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(name));
        var productUrl = created.Url.AbsolutePath;
        var withVariant = await admin.SubmitAsync($"{productUrl}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = $"PUB-{Guid.NewGuid():N}"[..20],
            ["Input.Attributes"] = "Tamanho: G",
            ["Input.Price"] = "49.90",
            ["Input.Stock"] = "0",
        });
        (await admin.PostAsync(productUrl, "publish", withVariant.AntiforgeryToken, [])).Html.ShouldContain("Publicado");
        return (productUrl, Slug(name));
    }

    private async Task<Dictionary<string, string>> Product(string name)
    {
        await using var scope = postgres.ShoppingWeb.Services.CreateAsyncScope();
        var category = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Categories.AsNoTracking()
            .OrderBy(c => c.Name).Select(c => c.Id).FirstAsync(Ct);

        return new()
        {
            ["Input.Name"] = name,
            ["Input.CategoryId"] = category.Value.ToString(),
            ["Input.Description"] = "Produto criado pelo painel.",
        };
    }

    private static string UniqueName() => $"Produto Painel {Guid.NewGuid():N}"[..28];

    private static string Slug(string name) => name.ToLowerInvariant().Replace(' ', '-');
}
