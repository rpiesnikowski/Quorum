using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.ApiScopes;

public class CreateModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public CreateModel(ConfigurationDbContext context) => _context = context;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Nazwa Scope jest wymagana")]
        [RegularExpression(@"^[a-zA-Z0-9_\.\:\-]+$", ErrorMessage = "Nazwa Scope może zawierać tylko litery, cyfry oraz znaki '.', '_', ':', '-' (bez spacji).")]
        [Display(Name = "Identyfikator Scope")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwa wyświetlana jest wymagana")]
        [Display(Name = "Nazwa wyświetlana")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Opis")]
        public string? Description { get; set; }

        [Display(Name = "Wymagany")]
        public bool Required { get; set; } = false;

        [Display(Name = "Wyróżniony")]
        public bool Emphasize { get; set; } = false;

        [Display(Name = "Aktywny")]
        public bool Enabled { get; set; } = true;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var normalizedName = Input.Name.Trim();

        // Sprawdzenie unikalności nazwy Scope przed próbą zapisu do bazy
        var exists = await _context.ApiScopes.AnyAsync(s => s.Name == normalizedName);
        if (exists)
        {
            ModelState.AddModelError("Input.Name", $"Zakres API o nazwie '{normalizedName}' już istnieje w bazie danych.");
            return Page();
        }

        var scope = new ApiScope
        {
            Name = normalizedName,
            DisplayName = Input.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            Required = Input.Required,
            Emphasize = Input.Emphasize,
            Enabled = Input.Enabled
        };

        try
        {
            _context.ApiScopes.Add(scope.ToEntity());
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            var isDuplicate = await _context.ApiScopes.AnyAsync(s => s.Name == normalizedName);
            if (isDuplicate)
            {
                ModelState.AddModelError("Input.Name", $"Zakres API o nazwie '{normalizedName}' już istnieje w bazie.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas zapisywania zakresu do bazy danych.");
            }
            return Page();
        }

        TempData["SuccessMessage"] = $"Zakres API '{normalizedName}' został pomyślnie dodany.";
        return RedirectToPage("Index");
    }
}
