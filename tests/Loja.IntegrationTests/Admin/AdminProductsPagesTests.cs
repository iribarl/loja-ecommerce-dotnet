using Loja.Catalog.Infrastructure.Persistence;
using Loja.IntegrationTests.Catalog;
using Loja.IntegrationTests.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Admin;

/// <summary>
/// Managing products in the panel, on the app instance whose catalog tests may change
/// (the catalog listing tests run on the other one).
/// </summary>
public sealed partial class AdminProductsPagesTests(PostgresFixture postgres)
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

    [Fact]
    public async Task AdjustStock_FromTheVariantPage_ShowsTheHistory()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(UniqueName()));
        var withVariant = await admin.SubmitAsync($"{created.Url.AbsolutePath}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = $"EST-{Guid.NewGuid():N}"[..20],
            ["Input.Attributes"] = "Tamanho: M",
            ["Input.Price"] = "30",
            ["Input.Stock"] = "1",
        });
        var variantUrl = VariantLink().Match(withVariant.Html).Value;

        var adjusted = await admin.SubmitAsync(variantUrl, "stock", new() { ["Stock.Quantity"] = "4", ["Stock.Reason"] = "Reposição do fornecedor" });
        adjusted.Html.ShouldContain("Estoque ajustado.");
        adjusted.Html.ShouldContain("Disponível agora: <strong>5</strong>");
        adjusted.Html.ShouldContain("Reposição do fornecedor");
        adjusted.Html.ShouldContain("+4");

        var tooMuch = await admin.SubmitAsync(variantUrl, "stock", new() { ["Stock.Quantity"] = "-6", ["Stock.Reason"] = "Avaria" });
        tooMuch.Html.ShouldContain("O estoque ficaria negativo: há 5 unidade(s) disponível(is).");

        var noReason = await admin.SubmitAsync(variantUrl, "stock", new() { ["Stock.Quantity"] = "1", ["Stock.Reason"] = "" });
        noReason.Html.ShouldContain("Informe o motivo.");
    }

    [Fact]
    public async Task UploadImage_ShowsInTheStore_AndRemoveDeletesIt()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var name = UniqueName();
        var (productUrl, slug) = await PublishedProduct(admin, name);
        var page = await admin.GetAsync(productUrl);

        var uploaded = await Upload(admin, $"{productUrl}/imagens", page.AntiforgeryToken, TestImages.Png(1600, 900), "foto.png");
        uploaded.Html.ShouldContain("Imagens atualizadas.");
        var imageUrl = UploadedImage().Match(uploaded.Html).Value; // the stored (largest) size
        imageUrl.ShouldNotBeEmpty();
        var sizes = SizeSuffixes.Select(suffix => imageUrl.Replace("-1200.webp", suffix, StringComparison.Ordinal)).ToList();

        // Every size is served as WebP, and the store offers them to the browser (srcset).
        foreach (var url in sizes)
        {
            var file = await admin.Client.GetAsync(url, Ct);
            file.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            file.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        }

        var store = await admin.GetAsync($"/produtos/{slug}");
        store.Html.ShouldContain($"{sizes[0]} 160w, {sizes[1]} 480w, {sizes[2]} 1200w");

        var removed = await admin.PostAsync($"{productUrl}/imagens/remover", formName: null, uploaded.AntiforgeryToken, new() { ["url"] = imageUrl });
        removed.Html.ShouldNotContain(imageUrl);
        foreach (var url in sizes)
        {
            (await admin.Client.GetAsync(url, Ct)).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task SeveralImages_MakeAGalleryInTheCardAndOnTheProductPage()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var name = UniqueName();
        var (productUrl, slug) = await PublishedProduct(admin, name);
        var page = await admin.GetAsync(productUrl);
        page = await Upload(admin, $"{productUrl}/imagens", page.AntiforgeryToken, TestImages.Png(800, 800), "a.png");
        await Upload(admin, $"{productUrl}/imagens", page.AntiforgeryToken, TestImages.Jpeg(600, 900), "b.jpg");

        var productPage = (await admin.GetAsync($"/produtos/{slug}")).Html;
        productPage.ShouldContain("data-gallery-next");
        productPage.ShouldContain("data-gallery-go=\"1\"");
        productPage.ShouldContain("Foto 2 de 2");
        // Full-screen view: every picture is clickable and the dialog holds them all.
        productPage.ShouldContain("<dialog id=\"product-lightbox\"");
        productPage.ShouldContain("data-lightbox=\"product-lightbox\"");
        System.Text.RegularExpressions.Regex.Count(productPage, "data-lightbox-open").ShouldBe(2);
        productPage.ShouldContain("</span> / 2</p>");

        var listing = (await admin.GetAsync($"/produtos?q={Uri.EscapeDataString(name)}")).Html;
        listing.ShouldContain($"Próxima foto de {name}");
        listing.ShouldContain("data-gallery-dot=\"1\"");
    }

    [Fact]
    public async Task UploadSomethingElse_IsRefused()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(UniqueName()));

        // An HTML page renamed to .png: the content decides, not the name.
        var refused = await Upload(admin, $"{created.Url.AbsolutePath}/imagens", created.AntiforgeryToken, "<html>oi</html>"u8.ToArray(), "foto.png");

        refused.Html.ShouldContain("Envie uma imagem JPG, PNG ou WebP.");
        UploadedImage().IsMatch(refused.Html).ShouldBeFalse();
    }

    [Fact]
    public async Task Upload_WithoutAntiforgeryToken_IsRejected()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", await Product(UniqueName()));

        var response = await admin.Client.PostAsync($"{created.Url.AbsolutePath}/imagens",
            ImageForm(TestImages.Png(100, 100), "foto.png", token: null), Ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    private static readonly string[] SizeSuffixes = ["-160.webp", "-480.webp", "-1200.webp"];

    private static async Task<BrowserSession.Page> Upload(BrowserSession browser, string url, string token, byte[] bytes, string fileName) =>
        await BrowserSession.Page.FromAsync(await browser.Client.PostAsync(url, ImageForm(bytes, fileName, token), Ct));

    private static MultipartFormDataContent ImageForm(byte[] bytes, string fileName, string? token)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "arquivo", fileName);
        if (token is not null)
        {
            form.Add(new StringContent(token), "__RequestVerificationToken");
        }

        return form;
    }

    [System.Text.RegularExpressions.GeneratedRegex("/uploads/produtos/[0-9a-f]{32}-1200\\.webp")]
    private static partial System.Text.RegularExpressions.Regex UploadedImage();

    [System.Text.RegularExpressions.GeneratedRegex("/admin/produtos/[0-9a-f-]{36}/variantes/[0-9a-f-]{36}")]
    private static partial System.Text.RegularExpressions.Regex VariantLink();

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
