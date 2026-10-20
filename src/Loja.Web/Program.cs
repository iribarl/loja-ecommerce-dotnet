using Loja.Api;
using Loja.Web.Components;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLojaApi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

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

app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapLojaApi();
app.MapDefaultEndpoints();

app.Run();
