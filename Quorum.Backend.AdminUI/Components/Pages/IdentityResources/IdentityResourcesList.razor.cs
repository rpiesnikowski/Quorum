using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.IdentityResources;

public partial class IdentityResourcesList : ComponentBase
{
    [Inject]
    public IAdminIdentityResourceStore IdentityResourceStore { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private RadzenDataGrid<IdentityResourceAdminModel>? grid;
    private IEnumerable<IdentityResourceAdminModel> resources = new List<IdentityResourceAdminModel>();
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
            var result = await IdentityResourceStore.GetResourcesAsync(searchTerm, 1, 1000);
            resources = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SeedStandardAsync()
    {
        var result = await IdentityResourceStore.SeedStandardResourcesAsync();
        if (result.Success)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Standardowe zasoby (openid, profile, email, address, phone) zostały zainicjowane.");
            await LoadDataAsync();
        }
    }

    private async Task ConfirmDeleteAsync(IdentityResourceAdminModel res)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz usunąć zasób tożsamości '{res.Name}'?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await IdentityResourceStore.DeleteResourceAsync(res.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Zasób '{res.Name}' został usunięty.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć zasobu.");
            }
        }
    }
}
