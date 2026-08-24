using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.EntityFramework.Data;

public interface IFederationDbContext
{
    DbSet<OidcFederationProvider> FederationProviders { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
