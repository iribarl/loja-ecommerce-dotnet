using Loja.Api;
using Loja.Cart.Infrastructure;
using Loja.Catalog.Infrastructure;
using Loja.Customers.Infrastructure;
using Loja.Orders.Infrastructure;
using Loja.Payments.Infrastructure;
using Loja.Shipping.Infrastructure;
using Loja.Web.Account;
using Loja.Web.Admin;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Loja.Web.Cart;
using Loja.Web.Orders;
using Loja.Web.Components;
using Loja.Web.Theming;
using Loja.Themes.Classico;
using Loja.Themes.Moderno;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Shared cache for every module: in-memory per instance, plus Redis as the second level when available.
// Without a Redis connection string (e.g. in tests) HybridCache keeps working in memory only.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString(RedisConnectionName)))
{
    builder.AddRedisDistributedCache(RedisConnectionName);

    // Same Redis cache, through its plain public class. The default one (RedisCacheImpl) asks the
    // container for HybridCache when it first connects, only to name the client "…HC". HybridCache
    // connects to Redis while it is being built, so that question re-enters its own construction and
    // the container ends up with two HybridCache singletons: invalidating one (RemoveByTagAsync) was
    // not seen by the other, and the store kept showing stale products.
    // See HybridCacheRedisTests.
    builder.Services.Replace(ServiceDescriptor.Singleton<IDistributedCache>(services =>
        new RedisCache(services.GetRequiredService<IOptions<RedisCacheOptions>>())));
}
builder.Services.AddHybridCache();

builder.AddCatalogModule();
builder.AddCartModule();
builder.AddCustomersModule();
builder.AddShippingModule();
builder.AddOrdersModule();
builder.AddPaymentsModule();

// Modules do not know each other; the host connects them: signing in merges the visitor's cart.
builder.Services.ConfigureApplicationCookie(cookie => cookie.Events.OnSignedIn = CartMergeOnSignIn.OnSignedInAsync);

// Store themes: the first is the default; others replace what they want and fall back to it.
builder.Services.AddLojaThemes(ClassicoTheme.Definition, ModernoTheme.Definition);

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLojaApi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The keys that sign the sign-in cookie and the antiforgery tokens. By default they live in memory
// (or in the user profile), so in a container every restart would sign everyone out and break forms
// open in the browser. "DataProtection:KeysPath" keeps them on a persistent volume.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Loja");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

// Migrations and demo data at start-up: on in Development, and wherever "Loja:Database:MigrateOnStartup"
// says so (the published demo). A real store would rather migrate in its deploy pipeline.
if (app.Configuration.GetValue("Loja:Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await app.Services.InitializeCatalogDatabaseAsync();
    await app.Services.InitializeCartDatabaseAsync();
    await app.Services.InitializeCustomersDatabaseAsync();
    await app.Services.InitializeOrdersDatabaseAsync();
    await app.Services.InitializePaymentsDatabaseAsync();
}

// One host serves two kinds of clients, so errors are rendered per client.
// Store pages (outer layer): HTML error and not-found pages.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// HTTP API (inner layer): ProblemDetails. It handles API errors first, so the outer
// page handlers find a response that is already written and leave it alone.
app.UseWhen(context => context.Request.Path.StartsWithSegments(ApiEndpoints.Prefix), api =>
{
    api.UseExceptionHandler();
    api.UseStatusCodePages();
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<CartCookie>();
app.UseMiddleware<ThemeSelection>();

app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Product images uploaded in the panel: files on disk, not part of the build's static assets.
var productImages = CatalogModule.ImagesPath(app.Configuration, app.Environment);
Directory.CreateDirectory(productImages);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productImages),
    RequestPath = CatalogModule.ImagesRequestPath,
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapLojaApi();
app.MapAccountEndpoints();
app.MapOrderEndpoints(app.Environment);
app.MapAdminImageEndpoints();
app.MapDefaultEndpoints();

app.Run();

// Makes Program visible to WebApplicationFactory in the integration tests.
public partial class Program
{
    /// <summary>Name of the Redis connection string injected by the Aspire AppHost.</summary>
    private const string RedisConnectionName = "redis";
}
