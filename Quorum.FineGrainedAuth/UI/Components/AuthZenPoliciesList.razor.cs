using Microsoft.AspNetCore.Components;
using Quorum.FineGrainedAuth.AuthZen.Models;
using Quorum.FineGrainedAuth.AuthZen.Stores;
using Radzen;
using Radzen.Blazor;

namespace Quorum.FineGrainedAuth.UI.Components;

public partial class AuthZenPoliciesList : ComponentBase
{
    [Inject]
    public IAuthZenPolicyStore PolicyStore { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private RadzenDataGrid<AuthZenPolicyAdminModel>? grid;
    private IEnumerable<AuthZenPolicyAdminModel> policies = new List<AuthZenPolicyAdminModel>();
    private bool isLoading = true;
    private string searchTerm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? "";
        await LoadDataAsync();
    }

    private async Task ResetFiltersAsync()
    {
        searchTerm = string.Empty;
        if (grid != null)
        {
            grid.Reset(true);
        }
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        isLoading = true;
        try
        {
            var result = await PolicyStore.GetPoliciesAsync(searchTerm, 1, 1000);
            policies = result.Items;
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd pobierania danych", ex.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ToggleStatusAsync(AuthZenPolicyAdminModel policy)
    {
        var result = await PolicyStore.TogglePolicyStatusAsync(policy.Id);
        if (result.Success)
        {
            policy.IsEnabled = !policy.IsEnabled;
            NotificationService.Notify(
                policy.IsEnabled ? NotificationSeverity.Success : NotificationSeverity.Warning,
                "Status polityki zmieniony",
                $"Polityka '{policy.Name}' jest teraz {(policy.IsEnabled ? "aktywna" : "wyłączona")}.");
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zmienić statusu.");
            await LoadDataAsync();
        }
    }

    private async Task ConfirmDeleteAsync(AuthZenPolicyAdminModel policy)
    {
        var confirmed = await DialogService.Confirm(
            $"Czy na pewno chcesz trwale usunąć politykę autoryzacyjną '{policy.Name}'?",
            "Potwierdzenie usunięcia",
            new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });

        if (confirmed == true)
        {
            var result = await PolicyStore.DeletePolicyAsync(policy.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Polityka '{policy.Name}' została usunięta.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć polityki.");
            }
        }
    }
}
