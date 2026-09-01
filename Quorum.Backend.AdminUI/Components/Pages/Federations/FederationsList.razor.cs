using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Federations;

public partial class FederationsList : ComponentBase
{
    [Inject]
    public IAdminFederationStore FederationStore { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private RadzenDataGrid<FederationAdminModel>? grid;
    private IEnumerable<FederationAdminModel> providers = new List<FederationAdminModel>();
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
            var result = await FederationStore.GetProvidersAsync(searchTerm, 1, 1000);
            providers = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ToggleStatusAsync(FederationAdminModel fed)
    {
        var result = await FederationStore.ToggleStatusAsync(fed.Id, !fed.IsEnabled);
        if (result.Success)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Sukces", !fed.IsEnabled ? "Dostawca został aktywowany." : "Dostawca został dezaktywowany.");
            await LoadDataAsync();
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zmienić statusu.");
        }
    }

    private async Task ConfirmDeleteAsync(FederationAdminModel fed)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz usunąć federację '{fed.DisplayName}' ({fed.Scheme})?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await FederationStore.DeleteProviderAsync(fed.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Federacja '{fed.DisplayName}' została usunięta.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć federacji.");
            }
        }
    }
}
