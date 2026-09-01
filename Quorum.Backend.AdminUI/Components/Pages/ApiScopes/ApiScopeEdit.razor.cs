using Microsoft.AspNetCore.Components;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.ApiScopes;

public partial class ApiScopeEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAdminApiScopeStore ApiScopeStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private bool IsNew => !Id.HasValue || Id.Value == 0;
    private ApiScopeAdminModel model = new();
    private bool isSubmitting = false;

    protected override async Task OnInitializedAsync()
    {
        if (!IsNew)
        {
            var sc = await ApiScopeStore.GetScopeByIdAsync(Id!.Value);
            if (sc != null)
            {
                model = sc;
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wskazanego zakresu.");
                NavigationManager.NavigateTo("admin/scopes");
            }
        }
        else
        {
            model = new ApiScopeAdminModel
            {
                Enabled = true,
                ShowInDiscoveryDocument = true
            };
        }
    }

    private async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var result = await ApiScopeStore.CreateScopeAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Zakres '{model.Name}' został utworzony.");
                    NavigationManager.NavigateTo("admin/scopes");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się utworzyć zakresu.");
                }
            }
            else
            {
                var result = await ApiScopeStore.UpdateScopeAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano dane zakresu.");
                    NavigationManager.NavigateTo("admin/scopes");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zaktualizować zakresu.");
                }
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
