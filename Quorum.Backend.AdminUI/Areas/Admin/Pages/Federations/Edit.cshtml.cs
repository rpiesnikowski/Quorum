using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Services;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Federations;

public class EditModel : PageModel
{
    private readonly IFederationAdminService _federationService;

    public EditModel(IFederationAdminService federationService)
    {
        _federationService = federationService;
    }

    [BindProperty]
    public OidcFederationProvider Input { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var fed = await _federationService.GetFederationByIdAsync(id);
        if (fed == null)
        {
            return NotFound();
        }

        Input = fed;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var success = await _federationService.UpdateFederationAsync(Input);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas aktualizacji dostawcy OIDC.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Zaktualizowano konfigurację dostawcy '{Input.DisplayName}'. Zmiany zostały natychmiast zsynchronizowane w pamięci serwera!";
        return RedirectToPage("Index");
    }
}
