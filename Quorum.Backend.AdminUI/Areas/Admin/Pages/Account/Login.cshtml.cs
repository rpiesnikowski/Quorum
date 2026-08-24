using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Options;
using Quorum.Backend.AdminUI.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IUserAdminService _userAdminService;
    private readonly AdminUiOptions _options;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IUserAdminService userAdminService,
        AdminUiOptions options,
        ILogger<LoginModel> logger)
    {
        _userAdminService = userAdminService;
        _options = options;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Wprowadź login lub adres email administratora.")]
        [Display(Name = "Login lub Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wprowadź hasło.")]
        [DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Zapamiętaj sesję")]
        public bool RememberMe { get; set; } = false;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/Admin");

        // Jeśli administrator jest już zalogowany w schemacie AdminCookie, przekieruj do panelu
        var authResult = await HttpContext.AuthenticateAsync(_options.AuthenticationScheme);
        if (authResult.Succeeded && authResult.Principal?.Identity?.IsAuthenticated == true)
        {
            if (authResult.Principal.IsInRole(_options.RequiredRole))
            {
                return LocalRedirect(returnUrl);
            }
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/Admin");

        if (!ModelState.IsValid)
        {
            ReturnUrl = returnUrl;
            return Page();
        }

        var validationResult = await _userAdminService.ValidateAdminCredentialsAsync(
            Input.Username,
            Input.Password,
            _options.RequiredRole);

        if (!validationResult.Succeeded || validationResult.User == null)
        {
            _logger.LogWarning("Nieudana próba logowania do panelu AdminUI dla konta: {Username}. Powód: {Reason}",
                Input.Username, validationResult.ErrorMessage);

            ModelState.AddModelError(string.Empty, validationResult.ErrorMessage ?? "Nieprawidłowy login lub hasło administratora.");
            ReturnUrl = returnUrl;
            return Page();
        }

        var user = validationResult.User;

        // Budujemy ClaimsPrincipal dedykowany dla schematu administracyjnego
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email)
        };

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            claims.Add(new Claim("name", user.FullName));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, _options.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.Add(_options.ExpireTimeSpan)
        };

        // Zalogowanie w dedykowanym schemacie ciasteczkowym Quorum.Admin.Auth
        await HttpContext.SignInAsync(_options.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("Administrator '{Username}' zalogował się pomyślnie do Quorum AdminUI (schemat: {Scheme}).",
            user.UserName, _options.AuthenticationScheme);

        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
