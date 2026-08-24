using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Open.IdentityServer.Models;
using Open.IdentityServer.Services;

namespace Quorum.Backend.Controllers;

[Authorize]
public class ConsentController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;

    public ConsentController(IIdentityServerInteractionService interaction)
    {
        _interaction = interaction;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        var request = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (request == null)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = new ConsentViewModel
        {
            ReturnUrl = returnUrl,
            ClientName = request.Client.ClientName ?? request.Client.ClientId,
            ClientUrl = request.Client.ClientUri,
            ClientLogoUrl = request.Client.LogoUri,
            AllowRememberConsent = request.Client.AllowRememberConsent,
            IdentityScopes = request.ValidatedResources.Resources.IdentityResources.Select(x => new ScopeViewModel
            {
                Value = x.Name,
                DisplayName = x.DisplayName ?? x.Name,
                Description = x.Description,
                Emphasize = x.Emphasize,
                Required = x.Required,
                Checked = true
            }).ToList(),
            ApiScopes = request.ValidatedResources.Resources.ApiScopes.Select(x => new ScopeViewModel
            {
                Value = x.Name,
                DisplayName = x.DisplayName ?? x.Name,
                Description = x.Description,
                Emphasize = x.Emphasize,
                Required = x.Required,
                Checked = true
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ConsentInputModel model)
    {
        var request = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);
        if (request == null)
        {
            return RedirectToAction("Index", "Home");
        }

        ConsentResponse? grantedConsent = null;

        if (model.Button == "no")
        {
            grantedConsent = new ConsentResponse { Error = AuthorizationError.AccessDenied };
        }
        else if (model.Button == "yes")
        {
            if (model.ScopesConsented != null && model.ScopesConsented.Any())
            {
                var scopes = model.ScopesConsented;
                grantedConsent = new ConsentResponse
                {
                    RememberConsent = model.RememberConsent,
                    ScopesValuesConsented = scopes.ToArray(),
                    Description = model.Description
                };
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Musisz wybrać przynajmniej jedno uprawnienie.");
            }
        }

        if (grantedConsent != null)
        {
            await _interaction.GrantConsentAsync(request, grantedConsent);
            return Redirect(model.ReturnUrl ?? "/");
        }

        return await Index(model.ReturnUrl);
    }
}

public class ConsentViewModel : ConsentInputModel
{
    public string? ClientName { get; set; }
    public string? ClientUrl { get; set; }
    public string? ClientLogoUrl { get; set; }
    public bool AllowRememberConsent { get; set; }
    public List<ScopeViewModel> IdentityScopes { get; set; } = new();
    public List<ScopeViewModel> ApiScopes { get; set; } = new();
}

public class ConsentInputModel
{
    public string? Button { get; set; }
    public IEnumerable<string>? ScopesConsented { get; set; }
    public bool RememberConsent { get; set; } = true;
    public string? ReturnUrl { get; set; }
    public string? Description { get; set; }
}

public class ScopeViewModel
{
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Emphasize { get; set; }
    public bool Required { get; set; }
    public bool Checked { get; set; }
}
