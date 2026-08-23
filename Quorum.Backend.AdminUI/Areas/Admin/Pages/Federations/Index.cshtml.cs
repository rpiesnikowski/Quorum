using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Federations;

public class IndexModel : PageModel
{
    private readonly IFederationAdminService _federationService;

    public IndexModel(IFederationAdminService federationService)
    {
        _federationService = federationService;
    }

    public List<OidcFederationProvider> Federations { get; set; } = new();

    public async Task OnGetAsync()
    {
        Federations = await _federationService.GetAllFederationsAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(string id)
    {
        var result = await _federationService.ToggleFederationStatusAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Zaktualizowano status aktywności dostawcy OIDC (zmiana natychmiastowa bez restartu serwera).";
        }
        else
        {
            TempData["ErrorMessage"] = "Nie udało się zmienić statusu dostawcy.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var result = await _federationService.DeleteFederationAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Dostawca OIDC został pomyślnie usunięty z bazy danych i wyrejestrowany z potoku autoryzacji.";
        }
        else
        {
            TempData["ErrorMessage"] = "Błąd podczas usuwania dostawcy OIDC.";
        }

        return RedirectToPage();
    }
}
