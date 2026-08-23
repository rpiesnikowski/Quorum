using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.Areas.Admin.Pages.ApiScopes;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public IndexModel(ConfigurationDbContext context) => _context = context;

    public IList<ApiScope> Scopes { get; set; } = default!;

    public async Task OnGetAsync()
    {
        Scopes = await _context.ApiScopes.AsNoTracking().ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var scope = await _context.ApiScopes.FindAsync(id);
        if (scope != null)
        {
            _context.ApiScopes.Remove(scope);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Zakres API '{scope.Name}' został usunięty.";
        }
        return RedirectToPage();
    }
}
