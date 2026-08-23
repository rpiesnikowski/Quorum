using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Open.IdentityServer.Events;
using Open.IdentityServer.Services;
using Quorum.Backend.Models;
using System.ComponentModel.DataAnnotations;
using Open.IdentityServer.Extensions;

namespace Quorum.Backend.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IIdentityServerInteractionService interaction,
        IEventService events)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _interaction = interaction;
        _events = events;
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
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Username = context?.LoginHint ?? string.Empty
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
                // Odmowa autoryzacji w kontekście OIDC
                await _interaction.DenyAuthorizationAsync(context, Open.IdentityServer.Models.AuthorizationError.AccessDenied);
                return Redirect(model.ReturnUrl ?? "/");
            }
            return Redirect("/");
        }

        if (!ModelState.IsValid)
        {
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
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        // Jeśli żądanie wylogowania pochodzi bezpośrednio z OIDC i nie wymaga monitu, wyloguj
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
