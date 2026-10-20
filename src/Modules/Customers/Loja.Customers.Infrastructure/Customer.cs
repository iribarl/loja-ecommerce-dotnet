using Microsoft.AspNetCore.Identity;

namespace Loja.Customers.Infrastructure;

/// <summary>
/// A store customer: an Identity user (email, password hash, lockout...) plus the few personal
/// data the store needs. LGPD: collect only what is necessary.
/// </summary>
public sealed class Customer : IdentityUser<Guid>
{
    public Customer()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = string.Empty;

    /// <summary>When the customer accepted the privacy policy (LGPD: proof of consent).</summary>
    public DateTimeOffset PrivacyPolicyAcceptedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>"Maria" for "Maria da Silva", to greet the customer.</summary>
    public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
}
