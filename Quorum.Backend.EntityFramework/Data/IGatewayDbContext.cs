using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.EntityFramework.Data;

public interface IGatewayDbContext
{
    DbSet<GatewayRoute> GatewayRoutes { get; }
    DbSet<GatewayRouteScope> GatewayRouteScopes { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
