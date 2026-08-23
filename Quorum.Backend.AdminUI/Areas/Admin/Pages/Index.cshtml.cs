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

    public IndexModel(
        ConfigurationDbContext configDb,
        PersistedGrantDbContext grantDb,
        IUserAdminService userService)
    {
        _configDb = configDb;
        _grantDb = grantDb;
        _userService = userService;
    }

    public int ClientsCount { get; set; }
    public int ScopesCount { get; set; }
    public int IdentityResourcesCount { get; set; }
    public int UsersCount { get; set; }
    public int GrantsCount { get; set; }

    public async Task OnGetAsync()
    {
        ClientsCount = await _configDb.Clients.CountAsync();
        ScopesCount = await _configDb.ApiScopes.CountAsync();
        IdentityResourcesCount = await _configDb.IdentityResources.CountAsync();
        GrantsCount = await _grantDb.PersistedGrants.CountAsync();
        UsersCount = await _userService.GetUsersCountAsync();
    }
}
