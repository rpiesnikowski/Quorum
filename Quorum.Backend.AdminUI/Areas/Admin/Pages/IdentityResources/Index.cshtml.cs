using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.IdentityResources;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public IndexModel(ConfigurationDbContext context) => _context = context;

    public IList<IdentityResource> Resources { get; set; } = default!;

    public async Task OnGetAsync()
    {
        Resources = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var res = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res != null)
        {
            _context.IdentityResources.Remove(res);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Zasób tożsamości '{res.Name}' został usunięty.";
        }

        return RedirectToPage();
    }
}
