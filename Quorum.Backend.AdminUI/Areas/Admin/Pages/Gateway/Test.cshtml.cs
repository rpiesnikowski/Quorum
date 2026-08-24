using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Gateway;

public class TestModel : PageModel
{
    private readonly IGatewayAdminService _gatewayService;

    public TestModel(IGatewayAdminService gatewayService)
    {
        _gatewayService = gatewayService;
    }

    [BindProperty]
    public GatewayTestRequest Input { get; set; } = new();

    public GatewayTestResponse? TestResult { get; set; }

    public List<GatewayRoute> AllRoutes { get; set; } = new();

    public async Task OnGetAsync(int? routeId, string? samplePath, string? sampleMethod)
    {
        AllRoutes = await _gatewayService.GetAllRoutesAsync();

        if (routeId.HasValue)
        {
            var route = AllRoutes.FirstOrDefault(r => r.Id == routeId.Value);
            if (route != null)
            {
                // Konfiguracja domyślnej ścieżki na podstawie wybranej reguły
                var sampleUrl = !string.IsNullOrEmpty(samplePath)
                    ? samplePath
                    : (!string.IsNullOrEmpty(route.AddressBasePath) ? route.AddressBasePath : "/api/v1/sample");

                Input.RequestUrl = sampleUrl;
                
                if (!string.IsNullOrEmpty(sampleMethod))
                {
                    Input.HttpMethod = sampleMethod.ToUpperInvariant();
                }
                else if (route.HttpMethods != "ALL" && !string.IsNullOrWhiteSpace(route.HttpMethods))
                {
                    var firstMethod = route.HttpMethods.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstMethod))
                    {
                        Input.HttpMethod = firstMethod.ToUpperInvariant();
                    }
                }

                if (!string.IsNullOrWhiteSpace(route.Headers))
                {
                    Input.RawHeaders = $"Accept: application/json\n{route.Headers}";
                }
            }
        }
        else if (!string.IsNullOrEmpty(samplePath))
        {
            Input.RequestUrl = samplePath;
            if (!string.IsNullOrEmpty(sampleMethod))
            {
                Input.HttpMethod = sampleMethod.ToUpperInvariant();
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AllRoutes = await _gatewayService.GetAllRoutesAsync();

        if (string.IsNullOrWhiteSpace(Input.RequestUrl))
        {
            ModelState.AddModelError(nameof(Input.RequestUrl), "Podaj ścieżkę wejściową lub adres URL.");
            return Page();
        }

        TestResult = await _gatewayService.ExecuteGatewayTestAsync(Input);

        return Page();
    }
}
