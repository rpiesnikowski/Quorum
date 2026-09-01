using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Grants;

public partial class GrantsList : ComponentBase
{
    [Inject]
    public IAdminGrantStore GrantStore { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private RadzenDataGrid<PersistedGrantAdminModel>? grid;
    private IEnumerable<PersistedGrantAdminModel> grants = new List<PersistedGrantAdminModel>();
    private bool isLoading = true;
    private string searchTerm = string.Empty;
    private string? selectedType;
    private List<string> grantTypes = new() { "refresh_token", "user_consent", "authorization_code", "reference_token", "device_code" };

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
        selectedType = null;
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
            var result = await GrantStore.GetGrantsAsync(searchTerm, selectedType, null, 1, 1000);
            grants = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ConfirmRevokeAsync(PersistedGrantAdminModel grant)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz unieważnić ten grant ({grant.Type}) dla klienta '{grant.ClientId}'?", "Unieważnienie grantu", new ConfirmOptions { OkButtonText = "Tak, unieważnij", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await GrantStore.RevokeGrantAsync(grant.Key);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Grant został unieważniony.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się unieważnić grantu.");
            }
        }
    }
}
