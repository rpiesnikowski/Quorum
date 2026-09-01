using Microsoft.AspNetCore.Components;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Federations;

public partial class FederationEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAdminFederationStore FederationStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private bool IsNew => !Id.HasValue || Id.Value == 0;
    private FederationAdminModel model = new();
    private DiscoveryValidationResult? discoveryResult;
    private bool isTestingDisco = false;
    private bool isSubmitting = false;

    protected override async Task OnInitializedAsync()
    {
        if (!IsNew)
        {
            var fed = await FederationStore.GetProviderByIdAsync(Id!.Value);
            if (fed != null)
            {
                model = fed;
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wskazanego dostawcy.");
                NavigationManager.NavigateTo("admin/federations");
            }
        }
        else
        {
            model = new FederationAdminModel
            {
                IsEnabled = true,
                AutoProvisionUsers = true,
                ResponseType = "code",
                Scopes = "openid profile email",
                CallbackPath = "/signin-oidc",
                SignedOutCallbackPath = "/signout-callback-oidc",
                DefaultRoles = "User"
            };
        }
    }

    private async Task TestDiscoveryEndpointAsync()
    {
        if (string.IsNullOrWhiteSpace(model.Authority))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Uwaga", "Wprowadź adres URL Authority przed testem.");
            return;
        }

        isTestingDisco = true;
        try
        {
            discoveryResult = await FederationStore.TestDiscoveryAsync(model.Authority);
        }
        finally
        {
            isTestingDisco = false;
        }
    }

    private async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var result = await FederationStore.CreateProviderAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Dostawca federacji '{model.DisplayName}' został utworzony.");
                    NavigationManager.NavigateTo("admin/federations");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się utworzyć dostawcy.");
                }
            }
            else
            {
                var result = await FederationStore.UpdateProviderAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano konfigurację dostawcy.");
                    NavigationManager.NavigateTo("admin/federations");
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
