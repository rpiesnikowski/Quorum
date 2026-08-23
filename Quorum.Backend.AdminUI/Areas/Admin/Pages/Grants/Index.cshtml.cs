using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Grants;

public class IndexModel : PageModel
{
    private readonly PersistedGrantDbContext _context;

    public IndexModel(PersistedGrantDbContext context) => _context = context;

    public IList<PersistedGrant> Grants { get; set; } = default!;

    public async Task OnGetAsync()
    {
        Grants = await _context.PersistedGrants.AsNoTracking().ToListAsync();
    }

    public async Task<IActionResult> OnPostRevokeAsync(long id, string? key = null)
    {
        var grant = await _context.PersistedGrants.FirstOrDefaultAsync(x => x.Id == id || (key != null && x.Key == key));
        if (grant != null)
        {
            _context.PersistedGrants.Remove(grant);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Zgoda / Token został unieważniony (Revoked).";
        }
        return RedirectToPage();
    }
}
