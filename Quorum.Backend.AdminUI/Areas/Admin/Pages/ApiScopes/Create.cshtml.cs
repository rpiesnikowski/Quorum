using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwa wyświetlana jest wymagana")]
        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool Required { get; set; } = false;
        public bool Emphasize { get; set; } = false;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var scope = new ApiScope
        {
            Name = Input.Name.Trim(),
            DisplayName = Input.DisplayName.Trim(),
            Description = Input.Description,
            Required = Input.Required,
            Emphasize = Input.Emphasize,
            Enabled = true
        };

        _context.ApiScopes.Add(scope.ToEntity());
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Zakres API '{Input.Name}' został pomyślnie dodany.";
        return RedirectToPage("Index");
    }
}
