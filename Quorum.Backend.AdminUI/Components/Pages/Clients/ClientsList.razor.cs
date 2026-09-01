using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Clients;

public partial class ClientsList : ComponentBase
{
    [Inject]
    public IAdminClientStore ClientStore { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private RadzenDataGrid<ClientAdminModel>? grid;
    private IEnumerable<ClientAdminModel> clients = new List<ClientAdminModel>();
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
            var result = await ClientStore.GetClientsAsync(searchTerm, 1, 1000);
            clients = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ConfirmDeleteAsync(ClientAdminModel client)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz bezpowrotnie usunąć klienta '{client.ClientId}'?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await ClientStore.DeleteClientAsync(client.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Klient '{client.ClientId}' został usunięty.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć klienta.");
            }
        }
    }
}
