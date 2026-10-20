using Loja.Customers.Contracts;
using Loja.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.Customers.Infrastructure;

internal sealed class CustomerDirectory(CustomersDbContext db) : ICustomerDirectory
{
    // Projects only the public fields: the password hash and security stamps never leave the module.
    public Task<CustomerInfo?> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerInfo(c.Id, c.FullName, c.Email!))
            .FirstOrDefaultAsync(cancellationToken);
}
