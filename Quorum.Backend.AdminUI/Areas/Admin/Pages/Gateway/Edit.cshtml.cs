using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Gateway;

public class EditModel : PageModel
{
    private readonly IGatewayAdminService _gatewayService;
    private readonly ConfigurationDbContext _configDb;

    public EditModel(
        IGatewayAdminService gatewayService,
        ConfigurationDbContext configDb)
    {
        _gatewayService = gatewayService;
        _configDb = configDb;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> AvailableScopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public class InputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Wzorzec dopasowania (Regex) jest wymagany.")]
        [StringLength(255, ErrorMessage = "Wzorzec Regex nie może przekraczać 255 znaków.")]
        [Display(Name = "Wzorzec dopasowania (MatchPattern Regex)")]
        public string MatchPattern { get; set; } = string.Empty;

        [StringLength(128)]
        [Display(Name = "Nazwa przyjazna trasy")]
        public string? RouteName { get; set; }

        [StringLength(512)]
        [Display(Name = "Opis funkcjonalny")]
        public string? Description { get; set; }

        // --- Segmenty URI ---

        [Required(ErrorMessage = "Schemat protokołu jest wymagany.")]
        [StringLength(16)]
        [Display(Name = "Protokół docelowy (Scheme)")]
        public string Scheme { get; set; } = "https";

        [Required(ErrorMessage = "Host docelowy (AddressHost) jest wymagany.")]
        [StringLength(255)]
        [Display(Name = "Host docelowy (AddressHost)")]
        public string AddressHost { get; set; } = string.Empty;

        [Required(ErrorMessage = "Port docelowy (AddressPort) jest wymagany.")]
        [Range(1, 65535, ErrorMessage = "Port musi zawierać się w przedziale 1 - 65535.")]
        [Display(Name = "Port docelowy (AddressPort)")]
        public int AddressPort { get; set; } = 443;

        [StringLength(255)]
        [Display(Name = "Ścieżka bazowa serwera docelowego (AddressBasePath)")]
        public string? AddressBasePath { get; set; }

        [StringLength(255)]
        [Display(Name = "Nadpisanie ścieżki docelowej (AddressPath)")]
        public string? AddressPath { get; set; }

        [StringLength(500)]
        [Display(Name = "Domyślny Query String (AddressQueryString)")]
        public string? AddressQueryString { get; set; }

        [Display(Name = "Nagłówki HTTP (JSON lub Key=Value)")]
        public string? Headers { get; set; }

        [Range(1, 600, ErrorMessage = "Timeout musi wynosić od 1 do 600 sekund.")]
        [Display(Name = "Limit czasu (Timeout w sekundach)")]
        public int TimeoutSeconds { get; set; } = 30;

        [StringLength(64)]
        [Display(Name = "Dozwolone metody HTTP")]
        public string HttpMethods { get; set; } = "ALL";

        // --- Zabezpieczenia & Uprawnienia ---

        [Display(Name = "Zezwól na dostęp anonimowy (AllowAnonymous - klasyczne proxy bez tokenu JWT)")]
        public bool AllowAnonymous { get; set; } = false;

        [Display(Name = "Wymagaj konkretnego Scope (RequiredScope)")]
        public bool RequiredScope { get; set; } = false;

        [Display(Name = "Wybierz powiązany Scope z bazy (Relacja FK ApiScopes)")]
        public int? ApiScopeId { get; set; }

        [StringLength(200)]
        [Display(Name = "Niestandardowa nazwa Scope")]
        public string? CustomScopeName { get; set; }

        [StringLength(255)]
        [Display(Name = "Schematy uwierzytelniania (np. Bearer, Cookies, entra-id)")]
        public string? AuthenticationSchemes { get; set; } = "Bearer";

        // --- Flagi konfiguracyjne ---

        [Display(Name = "Trasa aktywna (IsEnabled)")]
        public bool IsEnabled { get; set; } = true;

        [Range(-1000, 10000, ErrorMessage = "Priorytet musi być liczbą z zakresu -1000 do 10000.")]
        [Display(Name = "Priorytet dopasowania (Priority)")]
        public int Priority { get; set; } = 0;

        [Display(Name = "Włącz buforowanie odpowiedzi (Response Caching)")]
        public bool EnableCaching { get; set; } = false;

        [Display(Name = "Przekazuj oryginalny nagłówek Host")]
        public bool ForwardOriginalHost { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var route = await _gatewayService.GetRouteByIdAsync(id);
        if (route == null)
        {
            TempData["ErrorMessage"] = "Nie odnaleziono wskazanej trasy API Gateway.";
            return RedirectToPage("Index");
        }

        CreatedAt = route.CreatedAt;
        UpdatedAt = route.UpdatedAt;

        Input = new InputModel
        {
            Id = route.Id,
            MatchPattern = route.MatchPattern,
            RouteName = route.RouteName,
            Description = route.Description,
            Scheme = route.Scheme,
            AddressHost = route.AddressHost,
            AddressPort = route.AddressPort,
            AddressBasePath = route.AddressBasePath,
            AddressPath = route.AddressPath,
            AddressQueryString = route.AddressQueryString,
            Headers = route.Headers,
            TimeoutSeconds = route.TimeoutSeconds,
            HttpMethods = route.HttpMethods,
            AllowAnonymous = route.AllowAnonymous,
            RequiredScope = route.RequiredScope,
            ApiScopeId = route.ApiScopeId,
            CustomScopeName = route.ApiScopeId.HasValue ? null : route.ScopeName,
            AuthenticationSchemes = route.AuthenticationSchemes,
            IsEnabled = route.IsEnabled,
            Priority = route.Priority,
            EnableCaching = route.EnableCaching,
            ForwardOriginalHost = route.ForwardOriginalHost
        };

        await LoadAvailableScopesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAvailableScopesAsync();
            return Page();
        }

        // Walidacja poprawności wzorca Regex
        try
        {
            _ = new System.Text.RegularExpressions.Regex(Input.MatchPattern);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("Input.MatchPattern", $"Nieprawidłowy wzorzec wyrażenia regularnego Regex: {ex.Message}");
            await LoadAvailableScopesAsync();
            return Page();
        }

        string? resolvedScopeName = null;
        if (Input.RequiredScope)
        {
            if (Input.ApiScopeId.HasValue && Input.ApiScopeId.Value > 0)
            {
                var scope = await _configDb.ApiScopes.FindAsync(Input.ApiScopeId.Value);
                if (scope != null)
                {
                    resolvedScopeName = scope.Name;
                }
            }
            else if (!string.IsNullOrWhiteSpace(Input.CustomScopeName))
            {
                resolvedScopeName = Input.CustomScopeName.Trim();
            }
        }

        var route = new GatewayRoute
        {
            Id = Input.Id,
            MatchPattern = Input.MatchPattern.Trim(),
            RouteName = Input.RouteName?.Trim(),
            Description = Input.Description?.Trim(),
            Scheme = Input.Scheme.Trim().ToLowerInvariant(),
            AddressHost = Input.AddressHost.Trim(),
            AddressPort = Input.AddressPort,
            AddressBasePath = Input.AddressBasePath?.Trim(),
            AddressPath = Input.AddressPath?.Trim(),
            AddressQueryString = Input.AddressQueryString?.Trim(),
            Headers = Input.Headers?.Trim(),
            TimeoutSeconds = Input.TimeoutSeconds,
            HttpMethods = string.IsNullOrWhiteSpace(Input.HttpMethods) ? "ALL" : Input.HttpMethods.Trim(),
            AllowAnonymous = Input.AllowAnonymous,
            RequiredScope = Input.RequiredScope,
            ApiScopeId = (Input.RequiredScope && Input.ApiScopeId > 0) ? Input.ApiScopeId : null,
            ScopeName = resolvedScopeName,
            AuthenticationSchemes = Input.AllowAnonymous ? null : (string.IsNullOrWhiteSpace(Input.AuthenticationSchemes) ? "Bearer" : Input.AuthenticationSchemes.Trim()),
            IsEnabled = Input.IsEnabled,
            Priority = Input.Priority,
            EnableCaching = Input.EnableCaching,
            ForwardOriginalHost = Input.ForwardOriginalHost,
            UpdatedAt = DateTime.UtcNow
        };

        var success = await _gatewayService.UpdateRouteAsync(route);
        if (success)
        {
            TempData["SuccessMessage"] = $"Pomyślnie zaktualizowano trasę API Gateway '{route.MatchPattern}'.";
            return RedirectToPage("Index");
        }

        ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas aktualizacji trasy w bazie danych.");
        await LoadAvailableScopesAsync();
        return Page();
    }

    private async Task LoadAvailableScopesAsync()
    {
        var scopes = await _configDb.ApiScopes
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        AvailableScopes = scopes.Select(s => new SelectListItem
        {
            Value = s.Id.ToString(),
            Text = $"{s.Name} ({s.DisplayName})"
        }).ToList();

        AvailableScopes.Insert(0, new SelectListItem { Value = "", Text = "-- Wybierz ApiScope z bazy danych --" });
    }
}
