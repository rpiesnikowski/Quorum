using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Open.IdentityServer.Events;
using Open.IdentityServer.Extensions;
using Open.IdentityServer.Services;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.Models;
using Quorum.Backend.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Quorum.Backend.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly IDynamicOidcService _dynamicOidcService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IIdentityServerInteractionService interaction,
        IEventService events,
        IDynamicOidcService dynamicOidcService,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _interaction = interaction;
        _events = events;
        _dynamicOidcService = dynamicOidcService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // Jeśli użytkownik jest już zalogowany, przekieruj od razu do returnUrl lub panelu
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrEmpty(returnUrl) && (_interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl)))
            {
                return Redirect(returnUrl);
            }
            return Redirect("/Admin");
        }

        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        var activeFederations = await _dynamicOidcService.GetActiveFederationsAsync();

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Username = context?.LoginHint ?? string.Empty,
            ExternalProviders = activeFederations
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? button = null)
    {
        var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

        // Obsługa przycisku "Anuluj"
        if (button == "cancel")
        {
            if (context != null)
            {
                await _interaction.DenyAuthorizationAsync(context, Open.IdentityServer.Models.AuthorizationError.AccessDenied);
                return Redirect(model.ReturnUrl ?? "/");
            }
            return Redirect("/");
        }

        if (!ModelState.IsValid)
        {
            model.ExternalProviders = await _dynamicOidcService.GetActiveFederationsAsync();
            return View(model);
        }

        var user = await _userManager.FindByNameAsync(model.Username) ?? await _userManager.FindByEmailAsync(model.Username);
        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: context?.Client?.ClientId));

                if (!string.IsNullOrEmpty(model.ReturnUrl) && (_interaction.IsValidReturnUrl(model.ReturnUrl) || Url.IsLocalUrl(model.ReturnUrl)))
                {
                    return Redirect(model.ReturnUrl);
                }

                return Redirect("/Admin");
            }
        }

        await _events.RaiseAsync(new UserLoginFailureEvent(model.Username, "Nieprawidłowe dane uwierzytelniające", clientId: context?.Client?.ClientId));
        ModelState.AddModelError(string.Empty, "Nieprawidłowa nazwa użytkownika lub hasło.");
        model.ExternalProviders = await _dynamicOidcService.GetActiveFederationsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogError("Błąd zewnętrznego dostawcy OIDC: {Error}", remoteError);
            TempData["ErrorMessage"] = $"Błąd zewnętrznego dostawcy tożsamości: {remoteError}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Nie udało się pobrać informacji o logowaniu zewnętrznym.");
            TempData["ErrorMessage"] = "Nie udało się pobrać poświadczeń od zewnętrznego dostawcy tożsamości.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // Próba zalogowania jeśli konto jest już powiązane
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser != null)
            {
                await _events.RaiseAsync(new UserLoginSuccessEvent(existingUser.UserName, existingUser.Id, existingUser.UserName));
            }

            if (!string.IsNullOrEmpty(returnUrl) && (_interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl)))
            {
                return Redirect(returnUrl);
            }
            return Redirect("/Admin");
        }

        // Auto-provisioning nowego użytkownika na podstawie konfiguracji OidcFederationProvider
        var federation = await _dynamicOidcService.GetFederationBySchemeAsync(info.LoginProvider);
        if (federation == null || !federation.IsEnabled)
        {
            TempData["ErrorMessage"] = $"Dostawca tożsamości '{info.LoginProvider}' jest wyłączony lub nie istnieje.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!federation.AutoProvisionUsers)
        {
            TempData["ErrorMessage"] = $"Auto-rejestracja kont dla dostawcy '{federation.DisplayName}' jest wyłączona. Skontaktuj się z administratorem.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // Odczyt claimów użytkownika z tokenu OIDC
        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
            ?? info.Principal.FindFirstValue("email")
            ?? info.Principal.FindFirstValue("preferred_username")
            ?? $"{Guid.NewGuid():N}@external.local";

        var name = info.Principal.FindFirstValue(ClaimTypes.Name)
            ?? info.Principal.FindFirstValue("name")
            ?? email.Split('@')[0];

        // Sprawdź czy użytkownik o takim emailu już istnieje
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Błąd podczas tworzenia konta dla użytkownika z OIDC: {Errors}", errors);
                TempData["ErrorMessage"] = $"Błąd podczas tworzenia konta: {errors}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            // Nadanie domyślnej roli
            if (!string.IsNullOrEmpty(federation.DefaultRole))
            {
                if (!await _roleManager.RoleExistsAsync(federation.DefaultRole))
                {
                    await _roleManager.CreateAsync(new IdentityRole(federation.DefaultRole));
                }
                await _userManager.AddToRoleAsync(user, federation.DefaultRole);
            }
        }

        // Powiązanie konta z zewnętrznym dostawcą
        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (addLoginResult.Succeeded || (await _userManager.GetLoginsAsync(user)).Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName));

            if (!string.IsNullOrEmpty(returnUrl) && (_interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl)))
            {
                return Redirect(returnUrl);
            }
            return Redirect("/Admin");
        }

        TempData["ErrorMessage"] = "Wystąpił błąd podczas wiązania zewnętrznego konta OIDC z kontem Quorum.";
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        var context = await _interaction.GetLogoutContextAsync(logoutId);
        if (context?.ShowSignoutPrompt == false || User.Identity?.IsAuthenticated != true)
        {
            return await Logout(new LogoutViewModel { LogoutId = logoutId });
        }

        return View(new LogoutViewModel { LogoutId = logoutId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(LogoutViewModel model)
    {
        var logout = await _interaction.GetLogoutContextAsync(model.LogoutId);

        if (User.Identity?.IsAuthenticated == true)
        {
            await _signInManager.SignOutAsync();
            await _events.RaiseAsync(new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
        }

        var vm = new LoggedOutViewModel
        {
            PostLogoutRedirectUri = logout?.PostLogoutRedirectUri,
            ClientName = string.IsNullOrEmpty(logout?.ClientName) ? logout?.ClientId : logout?.ClientName,
            SignOutIFrameUrl = logout?.SignOutIFrameUrl,
            AutomaticRedirectAfterSignOut = true
        };

        return View("LoggedOut", vm);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Wprowadź nazwę użytkownika lub email")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wprowadź hasło")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }

    public List<OidcFederationProvider> ExternalProviders { get; set; } = new();
}

public class LogoutViewModel
{
    public string? LogoutId { get; set; }
}

public class LoggedOutViewModel
{
    public string? PostLogoutRedirectUri { get; set; }
    public string? ClientName { get; set; }
    public string? SignOutIFrameUrl { get; set; }
    public bool AutomaticRedirectAfterSignOut { get; set; } = true;
}
