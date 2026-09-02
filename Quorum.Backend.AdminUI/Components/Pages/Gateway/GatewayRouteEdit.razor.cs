using Microsoft.AspNetCore.Components;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Components.Pages.Gateway;

public partial class GatewayRouteEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAdminGatewayStore GatewayStore { get; set; } = default!;

    [Inject]
    public IAdminApiScopeStore ApiScopeStore { get; set; } = default!;

    [Inject]
    public IAdminIdentityResourceStore IdentityResourceStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public class ScopeItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } = "Zakres API";
        public bool IsIdentity { get; set; }
    }

    public class ApiScopeSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string DisplayNameOrName => string.IsNullOrWhiteSpace(DisplayName) ? Name : $"{DisplayName} ({Name})";
    }

    private bool IsNew => !Id.HasValue || Id.Value == 0;
    private GatewayRouteAdminModel model = new();
    private List<string> allHttpMethods = new() { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
    private List<string> schemesList = new() { "https", "http" };
    private List<ScopeItemModel> allAvailableScopesList = new();
    private List<ApiScopeSelectItem> dbApiScopesList = new();
    private string scopeSearchTerm = string.Empty;
    private bool isSubmitting = false;

    private string sampleBodyInput = "{\n  \"id\": 101,\n  \"name\": \"Jan Kowalski\",\n  \"email\": \"jan.kowalski@example.com\",\n  \"role\": \"Admin\"\n}";
    private string? sampleBodyOutput;
    private string? sampleBodyError;
    private string BodyPlaceholderText => model.BodyTransformType == "JUST" 
        ? "{\n  \"targetId\": \"#valueof($.id)\",\n  \"userName\": \"#valueof($.name)\"\n}" 
        : "{\n  \"userId\": {{ body.id }},\n  \"user\": \"{{ body.name }}\",\n  \"email\": \"{{ body.email }}\"\n}";

    private IEnumerable<ScopeItemModel> FilteredScopes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(scopeSearchTerm))
            {
                return allAvailableScopesList;
            }

            return allAvailableScopesList.Where(s => 
                s.Name.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.Description) && s.Description.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                s.Type.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // 1. Załaduj zakresy API i zasoby tożsamości
        var scopesRes = await ApiScopeStore.GetScopesAsync(pageSize: 500);
        var idRes = await IdentityResourceStore.GetResourcesAsync(pageSize: 500);

        dbApiScopesList = scopesRes.Items.Select(s => new ApiScopeSelectItem
        {
            Id = s.Id,
            Name = s.Name,
            DisplayName = s.DisplayName
        }).ToList();

        allAvailableScopesList = idRes.Items.Select(i => new ScopeItemModel
        {
            Name = i.Name,
            DisplayName = i.DisplayName,
            Description = i.Description,
            Type = "Zasób Tożsamości",
            IsIdentity = true
        })
        .Concat(scopesRes.Items.Select(s => new ScopeItemModel
        {
            Name = s.Name,
            DisplayName = s.DisplayName,
            Description = s.Description,
            Type = "Zakres API",
            IsIdentity = false
        }))
        .OrderBy(s => s.IsIdentity ? 0 : 1)
        .ThenBy(s => s.Name)
        .ToList();

        if (allAvailableScopesList.Count == 0)
        {
            allAvailableScopesList = new List<ScopeItemModel>
            {
                new() { Name = "openid", DisplayName = "OpenID Connect", Description = "Weryfikacja OIDC", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "profile", DisplayName = "Profil Użytkownika", Description = "Dane profilowe", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "email", DisplayName = "Adres E-mail", Description = "Adres email", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "quorum_api", DisplayName = "Quorum Core API", Description = "Główny interfejs API", Type = "Zakres API", IsIdentity = false },
                new() { Name = "gateway_routing", DisplayName = "Gateway Routing API", Description = "Dostęp do reguł i bramki proxy", Type = "Zakres API", IsIdentity = false },
                new() { Name = "orders.read", DisplayName = "Odczyt Zamówień", Description = "Uprawnienia odczytu zamówień", Type = "Zakres API", IsIdentity = false },
                new() { Name = "orders.write", DisplayName = "Modyfikacja Zamówień", Description = "Uprawnienia zapisu zamówień", Type = "Zakres API", IsIdentity = false }
            };
        }

        if (!IsNew)
        {
            var r = await GatewayStore.GetRouteByIdAsync(Id!.Value);
            if (r != null)
            {
                model = r;
                model.RequiredScopes ??= new List<string>();
                model.AllowedHttpMethods ??= new List<string> { "GET", "POST", "PUT", "DELETE" };
                model.BodyTransformType ??= "Fluid";
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wybranej trasy API Gateway.");
                NavigationManager.NavigateTo("admin/gateway");
            }
        }
        else
        {
            model = new GatewayRouteAdminModel
            {
                MatchPattern = "/api/v1/",
                RouteName = "Nowa Usługa",
                Priority = 10,
                IsEnabled = true,
                Scheme = "https",
                AddressHost = "localhost",
                AddressPort = 5001,
                AddressBasePath = "/api",
                BodyTransformType = "Fluid",
                TimeoutSeconds = 30,
                ForwardOriginalHost = true,
                EnableCaching = false,
                AllowAnonymous = false,
                RequiredScope = true,
                AuthenticationSchemes = "Bearer",
                AllowedHttpMethods = new List<string> { "GET", "POST", "PUT", "DELETE" },
                RequiredScopes = new List<string>()
            };
        }

        UpdateBodyPreview();
    }

    private void OnBodySettingsChanged()
    {
        UpdateBodyPreview();
        StateHasChanged();
    }

    private void OnBodyInputChanged(string? val)
    {
        model.Body = val;
        UpdateBodyPreview();
    }

    private void SetFluidPreset(int presetNum)
    {
        model.BodyTransformType = "Fluid";
        if (presetNum == 1)
        {
            model.Body = "{\n  \"userId\": {{ body.id }},\n  \"fullName\": \"{{ body.name }}\",\n  \"contactEmail\": \"{{ body.email }}\",\n  \"source\": \"Quorum-Gateway\"\n}";
        }
        else
        {
            model.Body = "{\n  \"userName\": \"{{ body.name }}\",\n  \"isAdmin\": {% if body.role == \"Admin\" %}true{% else %}false{% endif %}\n}";
        }
        UpdateBodyPreview();
        StateHasChanged();
    }

    private void SetJustPreset(int presetNum)
    {
        model.BodyTransformType = "JUST";
        if (presetNum == 1)
        {
            model.Body = "{\n  \"userId\": \"#valueof($.id)\",\n  \"fullName\": \"#valueof($.name)\",\n  \"contactEmail\": \"#valueof($.email)\",\n  \"source\": \"Quorum-Gateway\"\n}";
        }
        else
        {
            model.Body = "{\n  \"displayName\": \"#concat($.name, ' (', $.role, ')')\",\n  \"status\": \"#if($.id, 'Active', 'Inactive')\"\n}";
        }
        UpdateBodyPreview();
        StateHasChanged();
    }

    private void SetEmptyBodyPreset()
    {
        model.Body = "(empty)";
        UpdateBodyPreview();
        StateHasChanged();
    }

    private void ClearBody()
    {
        model.Body = null;
        UpdateBodyPreview();
        StateHasChanged();
    }

    private void UpdateBodyPreview()
    {
        if (string.IsNullOrWhiteSpace(model.Body))
        {
            sampleBodyOutput = "(brak transformacji - wejściowa treść zostanie przekazana do Upstream bez zmian)";
            sampleBodyError = null;
            return;
        }

        if (GatewayRouteMatcher.IsEmptyValue(model.Body))
        {
            sampleBodyOutput = "(empty - treść żądania zostanie usunięta przy przekazywaniu do Upstream)";
            sampleBodyError = null;
            return;
        }

        try
        {
            sampleBodyOutput = GatewayBodyTransformer.Transform(
                sampleBodyInput,
                model.Body,
                model.BodyTransformType,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "id", "101" }, { "version", "v1" } },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Authorization", "Bearer sample.token" } },
                out sampleBodyError);
        }
        catch (Exception ex)
        {
            sampleBodyError = ex.Message;
            sampleBodyOutput = null;
        }
    }

    private string GetCalculatedUpstreamPreview()
    {
        var scheme = !string.IsNullOrWhiteSpace(model.Scheme) ? model.Scheme : "https";
        var host = !string.IsNullOrWhiteSpace(model.AddressHost) ? model.AddressHost : "localhost";
        if (GatewayRouteMatcher.IsEmptyValue(host)) host = "localhost";

        var portStr = (model.AddressPort == 80 && scheme == "http") || (model.AddressPort == 443 && scheme == "https") ? "" : $":{model.AddressPort}";

        var basePath = !string.IsNullOrWhiteSpace(model.AddressBasePath) && !GatewayRouteMatcher.IsEmptyValue(model.AddressBasePath)
            ? (model.AddressBasePath.StartsWith("/") ? model.AddressBasePath : "/" + model.AddressBasePath)
            : "";

        string path;
        if (GatewayRouteMatcher.IsEmptyValue(model.AddressPath))
        {
            path = "";
        }
        else if (!string.IsNullOrWhiteSpace(model.AddressPath))
        {
            path = model.AddressPath.StartsWith("/") ? model.AddressPath : "/" + model.AddressPath;
        }
        else
        {
            path = "{ścieżka_żądania}";
        }

        var fullPath = $"{basePath}{path}";
        if (string.IsNullOrEmpty(fullPath)) fullPath = "/";

        var query = !string.IsNullOrWhiteSpace(model.AddressQueryString) && !GatewayRouteMatcher.IsEmptyValue(model.AddressQueryString)
            ? "?" + model.AddressQueryString.TrimStart('?')
            : "";

        return $"{scheme}://{host}{portStr}{fullPath}{query}";
    }

    private void SetMethodsPreset(string preset)
    {
        switch (preset)
        {
            case "ALL":
                model.AllowedHttpMethods = new List<string> { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                break;
            case "REST":
                model.AllowedHttpMethods = new List<string> { "GET", "POST", "PUT", "DELETE" };
                break;
            case "READ":
                model.AllowedHttpMethods = new List<string> { "GET", "HEAD" };
                break;
        }
        StateHasChanged();
    }

    private void SetLocalPreset()
    {
        model.Scheme = "http";
        model.AddressHost = "localhost";
        model.AddressPort = 5001;
        model.AddressBasePath = "/api";
        StateHasChanged();
    }

    private void SetK8sPreset()
    {
        model.Scheme = "http";
        model.AddressHost = "orders-service.default.svc.cluster.local";
        model.AddressPort = 8080;
        model.AddressBasePath = "/v1";
        StateHasChanged();
    }

    private void SetExternalPreset()
    {
        model.Scheme = "https";
        model.AddressHost = "api.external-service.com";
        model.AddressPort = 443;
        model.AddressBasePath = "";
        StateHasChanged();
    }

    private void AddScope(string scopeName)
    {
        if (string.IsNullOrWhiteSpace(scopeName)) return;

        model.RequiredScopes ??= new List<string>();

        if (!model.RequiredScopes.Contains(scopeName, StringComparer.OrdinalIgnoreCase))
        {
            model.RequiredScopes.Add(scopeName);
            model.RequiredScope = true;
            NotificationService.Notify(NotificationSeverity.Success, "Dodano Scope", $"Zakres '{scopeName}' został dodany jako wymagany dla tej trasy.");
            StateHasChanged();
        }
    }

    private void RemoveScope(string scopeName)
    {
        if (model.RequiredScopes != null && model.RequiredScopes.Remove(scopeName))
        {
            if (model.RequiredScopes.Count == 0)
            {
                model.RequiredScope = false;
            }
            NotificationService.Notify(NotificationSeverity.Info, "Usunięto Scope", $"Zakres '{scopeName}' został usunięty z listy wymaganych.");
            StateHasChanged();
        }
    }

    private void ClearAllScopes()
    {
        if (model.RequiredScopes != null && model.RequiredScopes.Count > 0)
        {
            model.RequiredScopes.Clear();
            model.RequiredScope = false;
            NotificationService.Notify(NotificationSeverity.Warning, "Wyczyszczono Zakresy", "Usunięto wszystkie wymagane zakresy dla tej trasy.");
            StateHasChanged();
        }
    }

    private void OnScopeRowClick(DataGridRowMouseEventArgs<ScopeItemModel> args)
    {
        if (args?.Data == null) return;
        var scopeName = args.Data.Name;

        model.RequiredScopes ??= new List<string>();

        if (!model.RequiredScopes.Contains(scopeName, StringComparer.OrdinalIgnoreCase))
        {
            model.RequiredScopes.Add(scopeName);
            model.RequiredScope = true;
            NotificationService.Notify(NotificationSeverity.Success, "Dodano Scope", $"Zakres '{scopeName}' został dodany.");
        }
        else
        {
            model.RequiredScopes.Remove(scopeName);
            if (model.RequiredScopes.Count == 0)
            {
                model.RequiredScope = false;
            }
            NotificationService.Notify(NotificationSeverity.Info, "Odpięto Scope", $"Zakres '{scopeName}' został odpięty.");
        }
        StateHasChanged();
    }

    private void NavigateToTester()
    {
        NavigationManager.NavigateTo("admin/gateway/test");
    }

    private async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var result = await GatewayStore.CreateRouteAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Trasa API Gateway została utworzona.");
                    NavigationManager.NavigateTo("admin/gateway");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się utworzyć trasy.");
                }
            }
            else
            {
                var result = await GatewayStore.UpdateRouteAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano trasę API Gateway.");
                    NavigationManager.NavigateTo("admin/gateway");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zapisać zmian.");
                }
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
