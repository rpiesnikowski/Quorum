using Microsoft.AspNetCore.Components;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.IdentityResources;

public partial class IdentityResourceEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAdminIdentityResourceStore IdentityResourceStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private bool IsNew => !Id.HasValue || Id.Value == 0;
    private IdentityResourceAdminModel model = new();
    private bool isSubmitting = false;

    protected override async Task OnInitializedAsync()
    {
        if (!IsNew)
        {
            var res = await IdentityResourceStore.GetResourceByIdAsync(Id!.Value);
            if (res != null)
            {
                model = res;
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wskazanego zasobu.");
                NavigationManager.NavigateTo("admin/identityresources");
            }
        }
        else
        {
            model = new IdentityResourceAdminModel
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
                var result = await IdentityResourceStore.CreateResourceAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Zasób '{model.Name}' został utworzony.");
                    NavigationManager.NavigateTo("admin/identityresources");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się utworzyć zasobu.");
                }
            }
            else
            {
                var result = await IdentityResourceStore.UpdateResourceAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano dane zasobu.");
                    NavigationManager.NavigateTo("admin/identityresources");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zaktualizować zasobu.");
                }
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
