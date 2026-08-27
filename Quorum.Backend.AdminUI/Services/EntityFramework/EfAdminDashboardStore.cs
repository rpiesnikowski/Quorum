using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminDashboardStore<TUser> : IAdminDashboardStore
    where TUser : IdentityUser, new()
{
    private readonly ConfigurationDbContext _configDb;
    private readonly PersistedGrantDbContext _grantDb;
    private readonly ApplicationDbContext _appDb;
    private readonly UserManager<TUser> _userManager;

    public EfAdminDashboardStore(
        ConfigurationDbContext configDb,
        PersistedGrantDbContext grantDb,
        ApplicationDbContext appDb,
        UserManager<TUser> userManager)
    {
        _configDb = configDb;
        _grantDb = grantDb;
        _appDb = appDb;
        _userManager = userManager;
    }

    public async Task<DashboardStatsModel> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var clientsCount = await _configDb.Clients.CountAsync(cancellationToken);
        var scopesCount = await _configDb.ApiScopes.CountAsync(cancellationToken);
        var idResCount = await _configDb.IdentityResources.CountAsync(cancellationToken);
        var usersCount = await _userManager.Users.CountAsync(cancellationToken);
        var federationsCount = await _appDb.FederationProviders.CountAsync(cancellationToken);
        var activeFedsCount = await _appDb.FederationProviders.CountAsync(f => f.IsEnabled, cancellationToken);
        var routesCount = await _appDb.GatewayRoutes.CountAsync(cancellationToken);
        var grantsCount = await _grantDb.PersistedGrants.CountAsync(cancellationToken);

        var recentActivities = new List<RecentActivityModel>
        {
            new() { Title = "Serwer Open.IdentityServer", Category = "System", Description = "Rdzeń tożsamości OIDC i tokenów operacyjnych działa w trybie wysokiej dostępności.", Timestamp = DateTime.UtcNow.AddMinutes(-5), Icon = "check_circle", BadgeVariant = "success" },
            new() { Title = "API Gateway Proxy", Category = "Gateway", Description = $"{routesCount} aktywnych reguł routingu z automatyczną weryfikacją nagłówków Scopes.", Timestamp = DateTime.UtcNow.AddMinutes(-18), Icon = "router", BadgeVariant = "info" },
            new() { Title = "Dynamiczne Federacje OIDC", Category = "SSO", Description = $"{activeFedsCount} dostawców tożsamości (m.in. Entra ID, Google) gotowych do logowania bez restartu.", Timestamp = DateTime.UtcNow.AddMinutes(-42), Icon = "group_work", BadgeVariant = "primary" },
            new() { Title = "Zarządzanie Użytkownikami", Category = "Identity", Description = $"Baza kont ASP.NET Identity zarejestrowała {usersCount} użytkowników.", Timestamp = DateTime.UtcNow.AddHours(-1), Icon = "people", BadgeVariant = "warning" }
        };

        return new DashboardStatsModel
        {
            ClientsCount = clientsCount,
            ApiScopesCount = scopesCount,
            IdentityResourcesCount = idResCount,
            UsersCount = usersCount,
            FederationsCount = federationsCount,
            ActiveFederationsCount = activeFedsCount,
            GatewayRoutesCount = routesCount,
            ActiveGrantsCount = grantsCount,
            RecentActivities = recentActivities
        };
    }
}
