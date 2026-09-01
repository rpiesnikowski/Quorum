using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Clients;

public partial class ClientEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAdminClientStore ClientStore { get; set; } = default!;

    [Inject]
    public IAdminApiScopeStore ApiScopeStore { get; set; } = default!;

    [Inject]
    public IAdminIdentityResourceStore IdentityResourceStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public class ScopeItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } = "Zakres API";
        public bool IsIdentity { get; set; }
        public bool Required { get; set; }
        public bool Emphasize { get; set; }
        public List<string> UserClaims { get; set; } = new();
    }

    private bool IsNew => !Id.HasValue || Id.Value == 0;
    private ClientAdminModel model = new();
    private List<string> availableGrantTypes = new() { "authorization_code", "client_credentials", "password", "implicit", "hybrid", "urn:ietf:params:oauth:grant-type:device_code" };
    private List<ScopeItemModel> allScopesList = new();
    private RadzenDataGrid<ScopeItemModel>? scopesGrid;
    private string scopeSearchTerm = string.Empty;
    private bool isSubmitting = false;

    private IEnumerable<ScopeItemModel> FilteredScopes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(scopeSearchTerm))
            {
                return allScopesList;
            }

            return allScopesList.Where(s => 
                s.Name.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.Description) && s.Description.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                s.Type.Contains(scopeSearchTerm, StringComparison.OrdinalIgnoreCase));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // Załaduj dostępne zakresy API i zasoby tożsamości z bazy danych
        var scopesRes = await ApiScopeStore.GetScopesAsync(pageSize: 500);
        var idRes = await IdentityResourceStore.GetResourcesAsync(pageSize: 500);
        
        allScopesList = idRes.Items.Select(i => new ScopeItemModel
        {
            Name = i.Name,
            DisplayName = i.DisplayName,
            Description = i.Description,
            Type = "Zasób Tożsamości",
            IsIdentity = true,
            Required = i.Required,
            Emphasize = i.Emphasize,
            UserClaims = i.UserClaims
        })
        .Concat(scopesRes.Items.Select(s => new ScopeItemModel
        {
            Name = s.Name,
            DisplayName = s.DisplayName,
            Description = s.Description,
            Type = "Zakres API",
            IsIdentity = false,
            Required = s.Required,
            Emphasize = s.Emphasize,
            UserClaims = s.UserClaims
        }))
        .OrderBy(s => s.IsIdentity ? 0 : 1)
        .ThenBy(s => s.Name)
        .ToList();

        if (allScopesList.Count == 0)
        {
            allScopesList = new List<ScopeItemModel>
            {
                new() { Name = "openid", DisplayName = "OpenID Connect", Description = "Wymagany do autoryzacji OIDC i wystawiania ID Tokena", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "profile", DisplayName = "Profil Użytkownika", Description = "Imię, nazwisko, preferencje konta", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "email", DisplayName = "Adres E-mail", Description = "Dostęp do adresu email i statusu weryfikacji", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "roles", DisplayName = "Role Użytkownika", Description = "Dostęp do ról i uprawnień RBAC", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "offline_access", DisplayName = "Dostęp Offline", Description = "Umożliwia wystawianie długoterminowych Refresh Tokenów", Type = "Zasób Tożsamości", IsIdentity = true },
                new() { Name = "quorum_api", DisplayName = "Quorum Core API", Description = "Główny interfejs API usług zaplecza", Type = "Zakres API", IsIdentity = false },
                new() { Name = "gateway_routing", DisplayName = "Gateway Routing API", Description = "Dostęp do reguł i bramki proxy", Type = "Zakres API", IsIdentity = false }
            };
        }

        if (!IsNew)
        {
            var client = await ClientStore.GetClientByIdAsync(Id!.Value);
            if (client != null)
            {
                model = client;
                model.AllowedScopes ??= new List<string>();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wskazanego klienta.");
                NavigationManager.NavigateTo("admin/clients");
            }
        }
        else
        {
            SetSpaTemplate();
        }
    }

    private void AddScope(string scopeName)
    {
        if (string.IsNullOrWhiteSpace(scopeName)) return;

        model.AllowedScopes ??= new List<string>();

        if (!model.AllowedScopes.Contains(scopeName, StringComparer.OrdinalIgnoreCase))
        {
            model.AllowedScopes.Add(scopeName);
            NotificationService.Notify(NotificationSeverity.Success, "Dodano Zakres", $"Zakres '{scopeName}' został dodany do klienta.");
            StateHasChanged();
        }
    }

    private void RemoveScope(string scopeName)
    {
        if (model.AllowedScopes != null && model.AllowedScopes.Remove(scopeName))
        {
            NotificationService.Notify(NotificationSeverity.Info, "Usunięto Zakres", $"Zakres '{scopeName}' został usunięty z klienta.");
            StateHasChanged();
        }
    }

    private void ClearAllScopes()
    {
        if (model.AllowedScopes != null && model.AllowedScopes.Count > 0)
        {
            model.AllowedScopes.Clear();
            NotificationService.Notify(NotificationSeverity.Warning, "Wyczyszczono Zakresy", "Usunięto wszystkie przypisane zakresy.");
            StateHasChanged();
        }
    }

    private void OnScopeRowClick(DataGridRowMouseEventArgs<ScopeItemModel> args)
    {
        if (args?.Data == null) return;
        var scopeName = args.Data.Name;

        model.AllowedScopes ??= new List<string>();

        if (!model.AllowedScopes.Contains(scopeName, StringComparer.OrdinalIgnoreCase))
        {
            model.AllowedScopes.Add(scopeName);
            NotificationService.Notify(NotificationSeverity.Success, "Dodano Zakres", $"Zakres '{scopeName}' został dodany do klienta.");
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Info, "Informacja", $"Zakres '{scopeName}' jest już dodany do tego klienta.");
        }
        StateHasChanged();
    }

    private void SetSpaTemplate()
    {
        model.AllowedGrantTypes = new List<string> { "authorization_code" };
        model.RequirePkce = true;
        model.RequireClientSecret = false;
        model.AllowedScopes = new List<string> { "openid", "profile", "email", "offline_access" };
        model.AllowOfflineAccess = true;
        StateHasChanged();
    }

    private void SetWebAppTemplate()
    {
        model.AllowedGrantTypes = new List<string> { "authorization_code" };
        model.RequirePkce = true;
        model.RequireClientSecret = true;
        model.AllowedScopes = new List<string> { "openid", "profile", "email", "offline_access" };
        model.AllowOfflineAccess = true;
        StateHasChanged();
    }

    private void SetM2MTemplate()
    {
        model.AllowedGrantTypes = new List<string> { "client_credentials" };
        model.RequirePkce = false;
        model.RequireClientSecret = true;
        model.AllowedScopes = new List<string> { "quorum_api" };
        model.AllowOfflineAccess = false;
        StateHasChanged();
    }

    private async Task RemoveSecretAsync(int secretId)
    {
        if (Id.HasValue)
        {
            var res = await ClientStore.DeleteSecretAsync(Id.Value, secretId);
            if (res.Success)
            {
                model.ClientSecrets.RemoveAll(s => s.Id == secretId);
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Sekret został usunięty.");
            }
        }
    }

    private async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var result = await ClientStore.CreateClientAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Klient '{model.ClientId}' został utworzony.");
                    NavigationManager.NavigateTo("admin/clients");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd tworzenia", result.Error ?? "Wystąpił błąd.");
                }
            }
            else
            {
                var result = await ClientStore.UpdateClientAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano dane klienta.");
                    NavigationManager.NavigateTo("admin/clients");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd aktualizacji", result.Error ?? "Wystąpił błąd.");
                }
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
