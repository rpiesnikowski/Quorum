using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Options;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly AdminUiOptions _options;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(AdminUiOptions options, ILogger<LogoutModel> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(_options.AuthenticationScheme);
        _logger.LogInformation("Administrator wylogował się ze schematu {Scheme}.", _options.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(_options.AuthenticationScheme);
        _logger.LogInformation("Administrator wylogował się ze schematu {Scheme}.", _options.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
