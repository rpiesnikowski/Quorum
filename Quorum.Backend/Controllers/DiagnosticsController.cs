using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quorum.Backend.Controllers;

[Authorize]
public class DiagnosticsController : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync();
        return View(new DiagnosticsViewModel
        {
            AuthenticateResult = authenticateResult
        });
    }
}

public class DiagnosticsViewModel
{
    public AuthenticateResult? AuthenticateResult { get; set; }
}
