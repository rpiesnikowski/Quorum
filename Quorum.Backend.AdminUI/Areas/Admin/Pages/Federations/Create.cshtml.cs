using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Services;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Federations;

public class CreateModel : PageModel
{
    private readonly IFederationAdminService _federationService;

    public CreateModel(IFederationAdminService federationService)
    {
        _federationService = federationService;
    }

    [BindProperty]
    public OidcFederationProvider Input { get; set; } = new()
    {
        Scheme = "custom-oidc",
        DisplayName = "Zewnętrzny Dostawca OIDC",
        Authority = "https://identity.example.com",
        ClientId = "my-client-id",
        ResponseType = "code",
        Scope = "openid profile email",
        CallbackPath = "/signin-oidc-custom",
        SignedOutCallbackPath = "/signout-callback-oidc",
        UsePkce = true,
        GetClaimsFromUserInfoEndpoint = true,
        SaveTokens = true,
        IsEnabled = true,
        AutoProvisionUsers = true,
        DefaultRole = "User",
        IconType = "openid"
    };

    [BindProperty(SupportsGet = true)]
    public string? Preset { get; set; }

    public void OnGet()
    {
        if (Preset == "entra")
        {
            Input.Scheme = "entra-id";
            Input.DisplayName = "Microsoft Entra ID";
            Input.Authority = "https://login.microsoftonline.com/organizations/v2.0";
            Input.ClientId = "00000000-0000-0000-0000-000000000000";
            Input.ResponseType = "code";
            Input.Scope = "openid profile email";
            Input.CallbackPath = "/signin-oidc-entra";
            Input.IconType = "microsoft";
            Input.ButtonColor = "#0078D4";
            Input.Prompt = "select_account";
        }
        else if (Preset == "azure-b2c")
        {
            Input.Scheme = "azure-b2c";
            Input.DisplayName = "Azure AD B2C";
            Input.Authority = "https://mytenant.b2clogin.com/mytenant.onmicrosoft.com/b2c_1_susi/v2.0/";
            Input.ClientId = "00000000-0000-0000-0000-000000000000";
            Input.ResponseType = "code";
            Input.Scope = "openid profile email";
            Input.CallbackPath = "/signin-oidc-b2c";
            Input.IconType = "azure";
            Input.ButtonColor = "#0089D6";
            Input.AdditionalParametersJson = "{\"p\": \"b2c_1_susi\"}";
        }
        else if (Preset == "google")
        {
            Input.Scheme = "google-oidc";
            Input.DisplayName = "Google Workspace";
            Input.Authority = "https://accounts.google.com";
            Input.ClientId = "000000000000-xxxxxx.apps.googleusercontent.com";
            Input.ResponseType = "code";
            Input.Scope = "openid profile email";
            Input.CallbackPath = "/signin-oidc-google";
            Input.IconType = "google";
            Input.ButtonColor = "#4285F4";
            Input.Prompt = "select_account";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var success = await _federationService.CreateFederationAsync(Input);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, $"Schemat o nazwie '{Input.Scheme}' już istnieje lub wystąpił błąd zapisu.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Dostawca OIDC '{Input.DisplayName}' ({Input.Scheme}) został pomyślnie zarejestrowany w pamięci bez restartu aplikacji!";
        return RedirectToPage("Index");
    }
}
