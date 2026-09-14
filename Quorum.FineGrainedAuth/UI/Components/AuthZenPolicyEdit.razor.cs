using Microsoft.AspNetCore.Components;
using Quorum.FineGrainedAuth.AuthZen.Models;
using Quorum.FineGrainedAuth.AuthZen.Stores;
using Radzen;

namespace Quorum.FineGrainedAuth.UI.Components;

public partial class AuthZenPolicyEdit : ComponentBase
{
    [Parameter]
    public int? Id { get; set; }

    [Inject]
    public IAuthZenPolicyStore PolicyStore { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    protected AuthZenPolicyAdminModel model = new();
    protected bool isSubmitting = false;

    protected bool IsNew => !Id.HasValue || Id.Value <= 0;

    protected override async Task OnInitializedAsync()
    {
        if (!IsNew)
        {
            var existing = await PolicyStore.GetPolicyByIdAsync(Id!.Value);
            if (existing != null)
            {
                model = existing;
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", $"Nie znaleziono polityki o ID {Id}.");
                NavigationManager.NavigateTo("admin/authzen/policies");
            }
        }
        else
        {
            model = new AuthZenPolicyAdminModel
            {
                IsEnabled = true,
                Priority = 100,
                Effect = "Permit",
                ResourceType = "route",
                ResourcePattern = "*",
                Action = "*",
                SubjectType = "user",
                RequireAuthenticated = true
            };
        }
    }

    protected async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var (success, error) = await PolicyStore.CreatePolicyAsync(model);
                if (success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Polityka '{model.Name}' została pomyślnie utworzona.");
                    NavigationManager.NavigateTo("admin/authzen/policies");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd zapisu", error ?? "Nie udało się utworzyć polityki.");
                }
            }
            else
            {
                var (success, error) = await PolicyStore.UpdatePolicyAsync(model);
                if (success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Polityka '{model.Name}' została zaktualizowana.");
                    NavigationManager.NavigateTo("admin/authzen/policies");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd aktualizacji", error ?? "Nie udało się zapisać zmian.");
                }
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Wyjątek", ex.Message);
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
