using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.IdentityResources;

public class EditModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public EditModel(ConfigurationDbContext context) => _context = context;

    [BindProperty]
    public IdentityResourceInputModel Input { get; set; } = new();

    public class IdentityResourceInputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nazwa zasobu jest wymagana.")]
        [Display(Name = "Nazwa Zasobu (Name)")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Nazwa Wyświetlana")]
        public string? DisplayName { get; set; }

        [Display(Name = "Opis")]
        public string? Description { get; set; }

        [Display(Name = "Włączony (Enabled)")]
        public bool Enabled { get; set; } = true;

        [Display(Name = "Wymagany (Required)")]
        public bool Required { get; set; } = false;

        [Display(Name = "Wyróżnij (Emphasize)")]
        public bool Emphasize { get; set; } = false;

        [Display(Name = "Pokaż w Discovery Document")]
        public bool ShowInDiscoveryDocument { get; set; } = true;

        public bool NonEditable { get; set; }

        public List<ExistingClaimModel> ExistingClaims { get; set; } = new();
        public string? NewClaimType { get; set; }
    }

    public class ExistingClaimModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool Delete { get; set; } = false;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var res = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
        {
            TempData["ErrorMessage"] = $"Nie znaleziono zasobu tożsamości o ID {id}.";
            return RedirectToPage("Index");
        }

        Input = new IdentityResourceInputModel
        {
            Id = res.Id,
            Name = res.Name,
            DisplayName = res.DisplayName,
            Description = res.Description,
            Enabled = res.Enabled,
            Required = res.Required,
            Emphasize = res.Emphasize,
            ShowInDiscoveryDocument = res.ShowInDiscoveryDocument,
            NonEditable = res.NonEditable,
            ExistingClaims = (res.UserClaims ?? new List<IdentityResourceClaim>()).Select(c => new ExistingClaimModel
            {
                Id = c.Id,
                Type = c.Type
            }).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var res = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .FirstOrDefaultAsync(r => r.Id == Input.Id);

        if (res == null)
        {
            TempData["ErrorMessage"] = "Zasób tożsamości nie istnieje.";
            return RedirectToPage("Index");
        }

        // Sprawdź czy nowa nazwa nie koliduje z innym zasobem
        var trimmedName = Input.Name.Trim().ToLowerInvariant();
        if (await _context.IdentityResources.AnyAsync(r => r.Id != Input.Id && r.Name.ToLower() == trimmedName))
        {
            ModelState.AddModelError("Input.Name", $"Inny zasób tożsamości o nazwie '{Input.Name}' już istnieje.");
            return Page();
        }

        res.Name = Input.Name.Trim();
        res.DisplayName = Input.DisplayName?.Trim();
        res.Description = Input.Description?.Trim();
        res.Enabled = Input.Enabled;
        res.Required = Input.Required;
        res.Emphasize = Input.Emphasize;
        res.ShowInDiscoveryDocument = Input.ShowInDiscoveryDocument;
        res.Updated = DateTime.UtcNow;

        // Obsługa usuwania claims
        if (Input.ExistingClaims != null && Input.ExistingClaims.Any())
        {
            var claimsToDelete = Input.ExistingClaims.Where(c => c.Delete).Select(c => c.Id).ToList();
            if (claimsToDelete.Any())
            {
                res.UserClaims.RemoveAll(c => claimsToDelete.Contains(c.Id));
            }
        }

        // Dodanie nowego claim
        if (!string.IsNullOrWhiteSpace(Input.NewClaimType))
        {
            var newTypes = Input.NewClaimType.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var type in newTypes)
            {
                if (!res.UserClaims.Any(c => c.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
                {
                    res.UserClaims.Add(new IdentityResourceClaim
                    {
                        Type = type
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Zasób tożsamości '{res.Name}' został pomyślnie zaktualizowany.";
        return RedirectToPage("Index");
    }
}
