using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Open.IdentityServer.Services;

namespace Quorum.Backend.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IWebHostEnvironment _environment;

    public HomeController(IIdentityServerInteractionService interaction, IWebHostEnvironment environment)
    {
        _interaction = interaction;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Error(string? errorId)
    {
        var vm = new ErrorViewModel();
        var message = await _interaction.GetErrorContextAsync(errorId);
        if (message != null)
        {
            vm.Error = message;
            if (!_environment.IsDevelopment())
            {
                message.ErrorDescription = null;
            }
        }
        return View("Error", vm);
    }
}

public class ErrorViewModel
{
    public Open.IdentityServer.Models.ErrorMessage? Error { get; set; }
}
