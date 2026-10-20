using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// HTTP API of the catalog, through the real app hosted in memory.
/// </summary>
public sealed class CatalogEndpointsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Client => postgres.Web.CreateClient();

    [Fact]
    public async Task SearchProducts_ReturnsPagedResult()
    {
        var json = await Client.GetFromJsonAsync<JsonElement>("/api/catalog/products?pageSize=5", Ct);

        json.GetProperty("totalCount").GetInt32().ShouldBe(21);
        json.GetProperty("items").GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public async Task SearchProducts_ByAttributes_UsesNameValuePairs()
    {
        var json = await Client.GetFromJsonAsync<JsonElement>(
            "/api/catalog/products?attr=Tamanho:GG&attr=Cor:Amarelo", Ct);

        json.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Jaqueta Corta-Vento");
    }

    [Theory]
    [InlineData("/api/catalog/products?attr=Tamanho", "attr")]
    [InlineData("/api/catalog/products?pageSize=1000", "PageSize")]
    public async Task SearchProducts_WithInvalidQuery_ReturnsValidationProblem(string url, string field)
    {
        var response = await Client.GetAsync(url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task SearchProducts_WithInvertedPriceRange_ReturnsBadRequest()
    {
        var response = await Client.GetAsync("/api/catalog/products?minPrice=100&maxPrice=50", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("Catalog.InvalidPriceRange");
    }

    [Fact]
    public async Task GetProduct_ReturnsVariantsAndSelectorOptions()
    {
        var json = await Client.GetFromJsonAsync<JsonElement>("/api/catalog/products/camisa-polo-piquet", Ct);

        json.GetProperty("product").GetProperty("variants").GetArrayLength().ShouldBe(12);
        json.GetProperty("attributeOptions").GetProperty("Tamanho").GetArrayLength().ShouldBe(4);
    }

    [Theory]
    [InlineData("nao-existe")]
    [InlineData("camiseta-edicao-limitada")]
    public async Task GetProduct_UnknownOrDraft_ReturnsNotFoundProblem(string slug)
    {
        var response = await Client.GetAsync($"/api/catalog/products/{slug}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("Catalog.ProductNotFound");
    }

    [Fact]
    public async Task ListCategories_ReturnsAll()
    {
        var json = await Client.GetFromJsonAsync<JsonElement>("/api/catalog/categories", Ct);

        json.GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public async Task GetFilterOptions_ForCategory_ReturnsItsAttributes()
    {
        var json = await Client.GetFromJsonAsync<JsonElement>("/api/catalog/filters?category=acessorios", Ct);

        json.GetProperty("categories").GetArrayLength().ShouldBe(5);
        json.GetProperty("attributes").GetProperty("Tamanho").GetArrayLength().ShouldBe(5);
    }
}
