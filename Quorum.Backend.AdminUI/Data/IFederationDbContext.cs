using Microsoft.EntityFrameworkCore;
using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Data;

public interface IFederationDbContext
{
    DbSet<OidcFederationProvider> FederationProviders { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
