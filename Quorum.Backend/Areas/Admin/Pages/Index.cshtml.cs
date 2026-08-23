using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.Models;

namespace Quorum.Backend.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _configDb;
    private readonly PersistedGrantDbContext _grantDb;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(
        ConfigurationDbContext configDb,
        PersistedGrantDbContext grantDb,
        UserManager<ApplicationUser> userManager)
    {
        _configDb = configDb;
        _grantDb = grantDb;
        _userManager = userManager;
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
        UsersCount = await _userManager.Users.CountAsync();
    }
}
