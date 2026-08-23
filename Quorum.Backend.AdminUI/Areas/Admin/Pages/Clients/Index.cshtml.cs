using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Clients;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public IndexModel(ConfigurationDbContext context)
    {
        _context = context;
    }

    public IList<Client> Clients { get; set; } = default!;

    public async Task OnGetAsync()
    {
        Clients = await _context.Clients
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client != null)
        {
            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Klient '{client.ClientId}' został pomyślnie usunięty.";
        }
        return RedirectToPage();
    }
}
