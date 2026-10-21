using Loja.Catalog.Application.Products;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.IntegrationTests.Admin;
using Loja.IntegrationTests.Customers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Redis;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// The catalog cache with a real Redis as its second level, as in the AppHost (the other app
/// instances in the tests run without Redis).
/// </summary>
/// <remarks>
/// Guards a real bug: with the default Redis cache, the container could build two HybridCache
/// singletons (see Program.cs), so a change made in the panel did not reach the store's cache.
/// </remarks>
public sealed class HybridCacheRedisTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:8.2").Build();

    private WebApplicationFactory<Program>? _app;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WebApplicationFactory<Program> App => _app ??= postgres.CreateApp("loja_redis", _redis.GetConnectionString());

    public async ValueTask InitializeAsync() => await _redis.StartAsync(Ct);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    [Fact]
    public void WithRedis_TheAppHasOneHybridCache()
    {
        var services = App.Services;
        using var scope = services.CreateScope();

        services.GetRequiredService<IDistributedCache>().ShouldBeOfType<RedisCache>();
        var cache = services.GetRequiredService<HybridCache>();
        services.GetServices<HybridCache>().ShouldHaveSingleItem().ShouldBeSameAs(cache);
        CacheInside(scope.ServiceProvider.GetRequiredService<ICatalogCacheInvalidator>()).ShouldBeSameAs(cache);
        CacheInside(scope.ServiceProvider.GetRequiredService<IProductQueries>()).ShouldBeSameAs(cache);
    }

    [Fact]
    public async Task WithRedis_ProductPageShowsAnImageAddedAfterItWasCached()
    {
        var admin = await AdminSession.SignInAsync(postgres, App);
        var (productUrl, slug) = await PublishedProduct(admin);

        (await admin.GetAsync($"/produtos/{slug}")).Html.ShouldNotContain("/uploads/produtos/"); // now cached

        var page = await admin.GetAsync(productUrl);
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(TestImages.Png(400, 400)), "arquivo", "foto.png" },
            { new StringContent(page.AntiforgeryToken), "__RequestVerificationToken" },
        };
        (await BrowserSession.Page.FromAsync(await admin.Client.PostAsync($"{productUrl}/imagens", form, Ct)))
            .Html.ShouldContain("Imagens atualizadas.");

        (await admin.GetAsync($"/produtos/{slug}")).Html.ShouldContain("/uploads/produtos/");
    }

    // The HybridCache a service was built with (its constructor parameter, kept in a field).
    private static object? CacheInside(object service) =>
        service.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(HybridCache))
            .GetValue(service);

    private async Task<(string ProductUrl, string Slug)> PublishedProduct(BrowserSession admin)
    {
        Guid category;
        await using (var scope = App.Services.CreateAsyncScope())
        {
            category = (await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Categories.AsNoTracking()
                .Select(c => c.Id).FirstAsync(Ct)).Value;
        }

        var name = $"Produto Redis {Guid.NewGuid():N}"[..27];
        var created = await admin.SubmitAsync("/admin/produtos/novo", "product", new()
        {
            ["Input.Name"] = name,
            ["Input.CategoryId"] = category.ToString(),
            ["Input.Description"] = "Cache com Redis.",
        });
        var productUrl = created.Url.AbsolutePath;
        var withVariant = await admin.SubmitAsync($"{productUrl}/variantes/nova", "variant", new()
        {
            ["Input.Sku"] = $"RED-{Guid.NewGuid():N}"[..20],
            ["Input.Attributes"] = "Tamanho: M",
            ["Input.Price"] = "20",
            ["Input.Stock"] = "0",
        });
        (await admin.PostAsync(productUrl, "publish", withVariant.AntiforgeryToken, [])).Html.ShouldContain("Publicado");
        return (productUrl, name.ToLowerInvariant().Replace(' ', '-'));
    }
}
