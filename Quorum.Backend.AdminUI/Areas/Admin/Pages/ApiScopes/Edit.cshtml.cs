using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.ApiScopes;

public class EditModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public EditModel(ConfigurationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var scope = await _context.ApiScopes.FirstOrDefaultAsync(s => s.Id == id);
        if (scope == null)
        {
            TempData["ErrorMessage"] = $"Nie znaleziono zakresu API o ID {id}.";
            return RedirectToPage("Index");
        }

        Input = new InputModel
        {
            Id = scope.Id,
            Name = scope.Name,
            DisplayName = scope.DisplayName,
            Description = scope.Description,
            Required = scope.Required,
            Emphasize = scope.Emphasize,
            Enabled = scope.Enabled
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var scope = await _context.ApiScopes.FirstOrDefaultAsync(s => s.Id == Input.Id);
        if (scope == null)
        {
            TempData["ErrorMessage"] = "Zakres API nie istnieje w bazie danych.";
            return RedirectToPage("Index");
        }

        var normalizedName = Input.Name.Trim();

        // Sprawdzenie czy inna encja nie ma już takiej samej nazwy Scope
        var duplicateExists = await _context.ApiScopes
            .AnyAsync(s => s.Name == normalizedName && s.Id != Input.Id);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Name", $"Inny zakres API posiada już identyfikator '{normalizedName}'.");
            return Page();
        }

        scope.Name = normalizedName;
        scope.DisplayName = Input.DisplayName.Trim();
        scope.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        scope.Required = Input.Required;
        scope.Emphasize = Input.Emphasize;
        scope.Enabled = Input.Enabled;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            var isDuplicate = await _context.ApiScopes.AnyAsync(s => s.Name == normalizedName && s.Id != Input.Id);
            if (isDuplicate)
            {
                ModelState.AddModelError("Input.Name", $"Inny zakres API posiada już identyfikator '{normalizedName}'.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas aktualizacji zakresu API w bazie danych.");
            }
            return Page();
        }

        TempData["SuccessMessage"] = $"Zakres API '{normalizedName}' został pomyślnie zaktualizowany.";
        return RedirectToPage("Index");
    }
}
