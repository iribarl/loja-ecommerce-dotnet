using Loja.Api;
using Loja.Cart.Infrastructure;
using Loja.Catalog.Infrastructure;
using Loja.Customers.Infrastructure;
using Loja.Orders.Infrastructure;
using Loja.Payments.Infrastructure;
using Loja.Shipping.Infrastructure;
using Loja.Web.Account;
using Loja.Web.Admin;
using Microsoft.Extensions.FileProviders;
using Loja.Web.Cart;
using Loja.Web.Orders;
using Loja.Web.Components;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Shared cache for every module: in-memory per instance, plus Redis as the second level when available.
// Without a Redis connection string (e.g. in tests) HybridCache keeps working in memory only.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString(RedisConnectionName)))
{
    builder.AddRedisDistributedCache(RedisConnectionName);
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

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLojaApi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Development only: production migrations are decided in phase 6.
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
