using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Data;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Gateway;

public class ScopesModel : PageModel
{
    private readonly IGatewayAdminService _gatewayService;
    private readonly ConfigurationDbContext _configDb;
    private readonly ApplicationDbContext _appDb;

    public ScopesModel(
        IGatewayAdminService gatewayService,
        ConfigurationDbContext configDb,
        ApplicationDbContext appDb)
    {
        _gatewayService = gatewayService;
        _configDb = configDb;
        _appDb = appDb;
    }

    public List<RouteScopeMappingDto> Mappings { get; set; } = new();
    public List<GatewayRoute> AllRoutes { get; set; } = new();
    public List<ScopeItemDto> AvailableScopes { get; set; } = new();

    // Filtry
    [BindProperty(SupportsGet = true)]
    public int? RouteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    // Statystyki
    public int TotalMappingsCount { get; set; }
    public int RoutesWithScopesCount { get; set; }
    public int AvailableScopesCount { get; set; }
    public int UnmappedRoutesCount { get; set; }

    // Model formularza dodawania pojedynczego powiązania
    [BindProperty]
    public AddScopeInputModel AddInput { get; set; } = new();

    public class AddScopeInputModel
    {
        [Required(ErrorMessage = "Wybierz trasę Gateway.")]
        [Display(Name = "Trasa Gateway")]
        public int RouteId { get; set; }

        [Display(Name = "Wybierz istniejący Zakres (Scope)")]
        public string? SelectedScope { get; set; }

        [Display(Name = "Lub wpisz niestandardowy Scope")]
        [StringLength(200, ErrorMessage = "Nazwa zakresu nie może przekraczać 200 znaków.")]
        public string? CustomScope { get; set; }
    }

    public class RouteScopeMappingDto
    {
        public int MappingId { get; set; }
        public int RouteId { get; set; }
        public string MatchPattern { get; set; } = string.Empty;
        public string? RouteName { get; set; }
        public string HttpMethods { get; set; } = "ALL";
        public string Upstream { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string ScopeType { get; set; } = "API";
        public string? ScopeDisplayName { get; set; }
        public string? ScopeDescription { get; set; }
        public bool RouteIsEnabled { get; set; }
        public bool RouteRequiredScope { get; set; }
        public bool RouteAllowAnonymous { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAddScopeAsync()
    {
        var scopeName = !string.IsNullOrWhiteSpace(AddInput.CustomScope)
            ? AddInput.CustomScope.Trim()
            : AddInput.SelectedScope?.Trim();

        if (string.IsNullOrWhiteSpace(scopeName))
        {
            TempData["ErrorMessage"] = "Musisz wybrać zakres z listy lub wpisać niestandardową nazwę zakresu.";
            await LoadDataAsync();
            return Page();
        }

        var success = await _gatewayService.AddScopeToRouteAsync(AddInput.RouteId, scopeName);
        if (success)
        {
            TempData["SuccessMessage"] = $"Pomyślnie przypisano zakres '{scopeName}' do trasy (ID: {AddInput.RouteId}).";
            return RedirectToPage(new { routeId = RouteId, searchTerm = SearchTerm });
        }

        TempData["ErrorMessage"] = "Wystąpił błąd podczas przypisywania zakresu do trasy.";
        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveScopeAsync(int routeId, string scopeName)
    {
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            TempData["ErrorMessage"] = "Nieprawidłowa nazwa zakresu.";
            return RedirectToPage(new { routeId = RouteId, searchTerm = SearchTerm });
        }

        var success = await _gatewayService.RemoveScopeFromRouteAsync(routeId, scopeName);
        if (success)
        {
            TempData["SuccessMessage"] = $"Pomyślnie usunięto powiązanie z zakresem '{scopeName}' dla trasy (ID: {routeId}).";
        }
        else
        {
            TempData["ErrorMessage"] = "Wystąpił błąd podczas usuwania powiązania.";
        }

        return RedirectToPage(new { routeId = RouteId, searchTerm = SearchTerm });
    }

    public async Task<IActionResult> OnPostDeleteMappingAsync(int mappingId)
    {
        var success = await _gatewayService.RemoveScopeMappingByIdAsync(mappingId);
        if (success)
        {
            TempData["SuccessMessage"] = "Pomyślnie usunięto powiązanie trasy z zakresem.";
        }
        else
        {
            TempData["ErrorMessage"] = "Nie odnaleziono lub nie udało się usunąć wskazanego powiązania.";
        }

        return RedirectToPage(new { routeId = RouteId, searchTerm = SearchTerm });
    }

    public async Task<IActionResult> OnPostSyncRouteScopesAsync(int routeId, string? scopes)
    {
        var scopeList = string.IsNullOrWhiteSpace(scopes)
            ? new List<string>()
            : scopes.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

        var success = await _gatewayService.SetRouteScopesAsync(routeId, scopeList);
        if (success)
        {
            TempData["SuccessMessage"] = $"Pomyślnie zsynchronizowano zakresy dla trasy (ID: {routeId}).";
        }
        else
        {
            TempData["ErrorMessage"] = "Wystąpił błąd podczas aktualizacji zakresów trasy.";
        }

        return RedirectToPage(new { routeId = RouteId, searchTerm = SearchTerm });
    }

    private async Task LoadDataAsync()
    {
        // 1. Pobierz wszystkie trasy
        AllRoutes = await _gatewayService.GetAllRoutesAsync();

        // 2. Pobierz zdefiniowane w systemie zakresy OIDC (Identity & ApiScopes)
        var identityScopes = await _configDb.IdentityResources
            .AsNoTracking()
            .Where(r => r.Enabled)
            .OrderBy(r => r.Name)
            .Select(r => new ScopeItemDto
            {
                Name = r.Name,
                DisplayName = r.DisplayName ?? r.Name,
                Description = r.Description,
                Type = "Tożsamość (Identity)",
                Emphasize = r.Emphasize
            })
            .ToListAsync();

        var apiScopes = await _configDb.ApiScopes
            .AsNoTracking()
            .Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .Select(s => new ScopeItemDto
            {
                Name = s.Name,
                DisplayName = s.DisplayName ?? s.Name,
                Description = s.Description,
                Type = "API Scope",
                Emphasize = s.Emphasize
            })
            .ToListAsync();

        AvailableScopes = identityScopes.Concat(apiScopes).ToList();
        AvailableScopesCount = AvailableScopes.Count;

        // 3. Pobierz wszystkie rekordy mapowań
        var rawMappings = await _appDb.GatewayRouteScopes
            .Include(s => s.GatewayRoute)
            .AsNoTracking()
            .ToListAsync();

        var scopeDictionary = AvailableScopes.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

        var dtoList = rawMappings.Select(m =>
        {
            var route = m.GatewayRoute;
            scopeDictionary.TryGetValue(m.Scope, out var matchedScope);

            return new RouteScopeMappingDto
            {
                MappingId = m.Id,
                RouteId = m.GatewayRouteId,
                MatchPattern = route?.MatchPattern ?? $"(Route #{m.GatewayRouteId})",
                RouteName = route?.RouteName,
                HttpMethods = route?.HttpMethods ?? "ALL",
                Upstream = route != null ? $"{route.Scheme}://{route.AddressHost}:{route.AddressPort}{route.AddressBasePath}" : "-",
                Scope = m.Scope,
                ScopeType = matchedScope?.Type ?? "Custom / External",
                ScopeDisplayName = matchedScope?.DisplayName,
                ScopeDescription = matchedScope?.Description,
                RouteIsEnabled = route?.IsEnabled ?? false,
                RouteRequiredScope = route?.RequiredScope ?? false,
                RouteAllowAnonymous = route?.AllowAnonymous ?? false,
                CreatedAt = route?.CreatedAt ?? DateTime.UtcNow
            };
        }).ToList();

        // 4. Oblicz statystyki ogólne
        TotalMappingsCount = dtoList.Count;
        RoutesWithScopesCount = AllRoutes.Count(r => r.Scopes.Any() || !string.IsNullOrWhiteSpace(r.ScopeName));
        UnmappedRoutesCount = AllRoutes.Count - RoutesWithScopesCount;

        // 5. Filtrowanie tabeli
        var query = dtoList.AsEnumerable();

        if (RouteId.HasValue && RouteId.Value > 0)
        {
            query = query.Where(m => m.RouteId == RouteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.Scope.ToLower().Contains(term) ||
                m.MatchPattern.ToLower().Contains(term) ||
                (m.RouteName != null && m.RouteName.ToLower().Contains(term)) ||
                (m.ScopeDisplayName != null && m.ScopeDisplayName.ToLower().Contains(term)) ||
                (m.Upstream.ToLower().Contains(term)));
        }

        Mappings = query
            .OrderBy(m => m.MatchPattern)
            .ThenBy(m => m.Scope)
            .ToList();
    }
}
