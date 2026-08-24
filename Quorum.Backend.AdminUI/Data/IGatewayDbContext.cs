using Microsoft.EntityFrameworkCore;
using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Data;

public interface IGatewayDbContext
{
    DbSet<GatewayRoute> GatewayRoutes { get; }
    DbSet<GatewayRouteScope> GatewayRouteScopes { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
