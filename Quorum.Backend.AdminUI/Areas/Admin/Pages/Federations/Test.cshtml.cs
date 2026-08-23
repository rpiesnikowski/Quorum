using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Federations;

public class TestModel : PageModel
{
    private readonly IFederationAdminService _federationService;

    public TestModel(IFederationAdminService federationService)
    {
        _federationService = federationService;
    }

    public OidcFederationProvider? Provider { get; set; }
    public OidcDiscoveryValidationResult? ValidationResult { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuthorityUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            Provider = await _federationService.GetFederationByIdAsync(id);
            if (Provider != null)
            {
                AuthorityUrl = Provider.Authority;
            }
        }

        if (!string.IsNullOrWhiteSpace(AuthorityUrl))
        {
            ValidationResult = await _federationService.ValidateDiscoveryDocumentAsync(AuthorityUrl);
        }

        return Page();
    }
}
