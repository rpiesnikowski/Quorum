using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Gateway;

public partial class GatewayRoutesList : ComponentBase
{
    [Inject]
    public IAdminGatewayStore GatewayStore { get; set; } = default!;

    [Inject]
    public IAdminImportExportService ImportExportService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private RadzenDataGrid<GatewayRouteAdminModel>? grid;
    private IEnumerable<GatewayRouteAdminModel> routes = new List<GatewayRouteAdminModel>();
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
            var result = await GatewayStore.GetRoutesAsync(searchTerm, 1, 1000);
            routes = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ConfirmDeleteAsync(GatewayRouteAdminModel route)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz usunąć trasę '{route.PathPattern}'?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await GatewayStore.DeleteRouteAsync(route.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Trasa API Gateway została usunięta.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć trasy.");
            }
        }
    }

    private async Task ExportJsonAsync()
    {
        try
        {
            var json = await ImportExportService.ExportGatewayRoutesJsonAsync();
            var fileName = $"quorum-gateway-routes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await FileDownloadHelper.DownloadJsonFileAsync(JSRuntime, fileName, json);
            NotificationService.Notify(NotificationSeverity.Success, "Eksport zakończony", $"Pomyślnie wyeksportowano trasy API Gateway do pliku {fileName}");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd eksportu", ex.Message);
        }
    }

    private async Task OpenImportDialogAsync()
    {
        var result = await DialogService.OpenAsync<DataImportDialog>(
            "Import Tras API Gateway (JSON)",
            new Dictionary<string, object>
            {
                { "EntityType", ImportEntityType.GatewayRoutes }
            },
            new DialogOptions { Width = "750px", Resizable = true, Draggable = true });

        if (result is DataImportResult importResult && importResult.Success)
        {
            await LoadDataAsync();
        }
    }
}
