using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.IdentityResources;

public class CreateModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public CreateModel(ConfigurationDbContext context) => _context = context;

    [BindProperty]
    public IdentityResourceInputModel Input { get; set; } = new();

    public class IdentityResourceInputModel
    {
        [Required(ErrorMessage = "Nazwa zasobu (np. profile, custom_profile) jest wymagana.")]
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

        [Display(Name = "Skojarzone Claims")]
        public string? UserClaims { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var trimmedName = Input.Name.Trim().ToLowerInvariant();
        if (await _context.IdentityResources.AnyAsync(r => r.Name.ToLower() == trimmedName))
        {
            ModelState.AddModelError("Input.Name", $"Zasób tożsamości o nazwie '{Input.Name}' już istnieje.");
            return Page();
        }

        var entity = new IdentityResource
        {
            Name = Input.Name.Trim(),
            DisplayName = Input.DisplayName?.Trim(),
            Description = Input.Description?.Trim(),
            Enabled = Input.Enabled,
            Required = Input.Required,
            Emphasize = Input.Emphasize,
            ShowInDiscoveryDocument = Input.ShowInDiscoveryDocument,
            Created = DateTime.UtcNow,
            UserClaims = new List<IdentityResourceClaim>()
        };

        if (!string.IsNullOrWhiteSpace(Input.UserClaims))
        {
            var claims = Input.UserClaims.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var claim in claims)
            {
                entity.UserClaims.Add(new IdentityResourceClaim
                {
                    Type = claim
                });
            }
        }

        _context.IdentityResources.Add(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Zasób tożsamości '{entity.Name}' został pomyślnie utworzony.";
        return RedirectToPage("Index");
    }
}
