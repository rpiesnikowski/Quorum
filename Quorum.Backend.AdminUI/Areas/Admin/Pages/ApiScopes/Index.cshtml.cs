using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.ApiScopes;

public class IndexModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public IndexModel(ConfigurationDbContext context) => _context = context;

    public IList<ApiScope> Scopes { get; set; } = new List<ApiScope>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; } // "all", "active", "disabled"

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int ActiveCount { get; set; }
    public int DisabledCount { get; set; }

    public async Task OnGetAsync()
    {
        if (CurrentPage < 1) CurrentPage = 1;
        if (PageSize < 1) PageSize = 10;

        var baseQuery = _context.ApiScopes.AsNoTracking();

        ActiveCount = await baseQuery.CountAsync(s => s.Enabled);
        DisabledCount = await baseQuery.CountAsync(s => !s.Enabled);

        var query = baseQuery;

        // Filtrowanie po tekście (nazwa, nazwa wyświetlana, opis)
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(search) ||
                (s.DisplayName != null && s.DisplayName.ToLower().Contains(search)) ||
                (s.Description != null && s.Description.ToLower().Contains(search)));
        }

        // Filtrowanie po statusie
        if (StatusFilter == "active")
        {
            query = query.Where(s => s.Enabled);
        }
        else if (StatusFilter == "disabled")
        {
            query = query.Where(s => !s.Enabled);
        }

        TotalItems = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
        if (TotalPages > 0 && CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        Scopes = await query
            .OrderBy(s => s.Name)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
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
        return RedirectToPage(new { search = Search, statusFilter = StatusFilter, currentPage = CurrentPage, pageSize = PageSize });
    }
}
