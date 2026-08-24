using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _configDb;
    private readonly PersistedGrantDbContext _grantDb;
    private readonly IUserAdminService _userService;
    private readonly IFederationAdminService _federationService;
    private readonly IGatewayAdminService _gatewayService;

    public IndexModel(
        ConfigurationDbContext configDb,
        PersistedGrantDbContext grantDb,
        IUserAdminService userService,
        IFederationAdminService federationService,
        IGatewayAdminService gatewayService)
    {
        _configDb = configDb;
        _grantDb = grantDb;
        _userService = userService;
        _federationService = federationService;
        _gatewayService = gatewayService;
    }

    public int ClientsCount { get; set; }
    public int ScopesCount { get; set; }
    public int IdentityResourcesCount { get; set; }
    public int UsersCount { get; set; }
    public int GrantsCount { get; set; }
    public int FederationsCount { get; set; }
    public int GatewayRoutesCount { get; set; }

    public async Task OnGetAsync()
    {
        ClientsCount = await _configDb.Clients.CountAsync();
        ScopesCount = await _configDb.ApiScopes.CountAsync();
        IdentityResourcesCount = await _configDb.IdentityResources.CountAsync();
        GrantsCount = await _grantDb.PersistedGrants.CountAsync();
        UsersCount = await _userService.GetUsersCountAsync();
        FederationsCount = await _federationService.GetFederationsCountAsync();
        var (total, _, _, _) = await _gatewayService.GetStatisticsAsync();
        GatewayRoutesCount = total;
    }
}
