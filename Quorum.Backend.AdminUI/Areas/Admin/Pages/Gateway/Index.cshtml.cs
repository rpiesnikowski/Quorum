using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Gateway;

public class IndexModel : PageModel
{
    private readonly IGatewayAdminService _gatewayService;

    public IndexModel(IGatewayAdminService gatewayService)
    {
        _gatewayService = gatewayService;
    }

    public GatewayPagedResult<GatewayRoute> RoutesResult { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuthFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public int TotalRoutes { get; set; }
    public int EnabledRoutes { get; set; }
    public int AnonymousRoutes { get; set; }
    public int ProtectedRoutes { get; set; }

    public async Task OnGetAsync()
    {
        bool? isEnabled = null;
        if (StatusFilter == "enabled") isEnabled = true;
        else if (StatusFilter == "disabled") isEnabled = false;

        bool? allowAnonymous = null;
        if (AuthFilter == "anonymous") allowAnonymous = true;
        else if (AuthFilter == "protected") allowAnonymous = false;

        RoutesResult = await _gatewayService.GetRoutesPagedAsync(
            SearchTerm,
            isEnabled,
            allowAnonymous,
            PageIndex,
            PageSize);

        var stats = await _gatewayService.GetStatisticsAsync();
        TotalRoutes = stats.Total;
        EnabledRoutes = stats.Enabled;
        AnonymousRoutes = stats.Anonymous;
        ProtectedRoutes = stats.Protected;
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(int id)
    {
        var result = await _gatewayService.ToggleRouteStatusAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Zmieniono status aktywności trasy API Gateway.";
        }
        else
        {
            TempData["ErrorMessage"] = "Nie udało się zmienić statusu trasy.";
        }

        return RedirectToPage(new { SearchTerm, StatusFilter, AuthFilter, PageIndex, PageSize });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var result = await _gatewayService.DeleteRouteAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Trasa API Gateway została pomyślnie usunięta.";
        }
        else
        {
            TempData["ErrorMessage"] = "Błąd podczas usuwania trasy API Gateway.";
        }

        return RedirectToPage(new { SearchTerm, StatusFilter, AuthFilter, PageIndex, PageSize });
    }
}
