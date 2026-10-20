using Loja.Customers.Contracts;
using Loja.Customers.Infrastructure.Persistence;
using Loja.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Customers.Infrastructure;

/// <summary>
/// Entry point of the Customers module: ASP.NET Core Identity with a cookie, stored in Postgres.
/// </summary>
public static class CustomersModule
{
    public const string LoginPath = "/conta/entrar";
    public const string ReturnUrlParameter = "returnUrl";

    public static IHostApplicationBuilder AddCustomersModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<CustomersDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(LojaDatabase.ConnectionStringName), CustomersDbContext.ConfigureNpgsql));
        builder.EnrichNpgsqlDbContext<CustomersDbContext>();

        builder.Services.AddScoped<ICustomerDirectory, CustomerDirectory>();

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(cookie =>
        {
            cookie.LoginPath = LoginPath;
            cookie.ReturnUrlParameter = ReturnUrlParameter;
            cookie.Cookie.Name = "loja_auth";
            cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
            cookie.SlidingExpiration = true;
        });

        builder.Services
            .AddIdentityCore<Customer>(options =>
            {
                // NIST SP 800-63B: length protects more than forced complexity ("Senha@123").
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.User.RequireUniqueEmail = true;

                // Slows down password guessing: 5 failures lock the account for 5 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                // No email service yet: accounts can sign in without confirming the address.
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<CustomersDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<CustomerClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        return builder;
    }

    /// <summary>Applies pending migrations. Development only.</summary>
    public static async Task InitializeCustomersDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CustomersModule));

        await db.MigrateWithRetryAsync(logger, cancellationToken);
    }
}
