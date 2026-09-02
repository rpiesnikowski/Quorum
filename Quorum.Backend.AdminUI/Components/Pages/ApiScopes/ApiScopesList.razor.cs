using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.ApiScopes;

public partial class ApiScopesList : ComponentBase
{
    [Inject]
    public IAdminApiScopeStore ApiScopeStore { get; set; } = default!;

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

    private RadzenDataGrid<ApiScopeAdminModel>? grid;
    private IEnumerable<ApiScopeAdminModel> scopes = new List<ApiScopeAdminModel>();
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
            var result = await ApiScopeStore.GetScopesAsync(searchTerm, 1, 1000);
            scopes = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ConfirmDeleteAsync(ApiScopeAdminModel scope)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz usunąć zakres API '{scope.Name}'?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await ApiScopeStore.DeleteScopeAsync(scope.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Zakres '{scope.Name}' został usunięty.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć zakresu.");
            }
        }
    }

    private async Task ExportJsonAsync()
    {
        try
        {
            var json = await ImportExportService.ExportApiScopesJsonAsync();
            var fileName = $"quorum-api-scopes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await FileDownloadHelper.DownloadJsonFileAsync(JSRuntime, fileName, json);
            NotificationService.Notify(NotificationSeverity.Success, "Eksport zakończony", $"Pomyślnie wyeksportowano zakresy API do pliku {fileName}");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd eksportu", ex.Message);
        }
    }

    private async Task OpenImportDialogAsync()
    {
        var result = await DialogService.OpenAsync<DataImportDialog>(
            "Import Zakresów API (JSON)",
            new Dictionary<string, object>
            {
                { "EntityType", ImportEntityType.ApiScopes }
            },
            new DialogOptions { Width = "750px", Resizable = true, Draggable = true });

        if (result is DataImportResult importResult && importResult.Success)
        {
            await LoadDataAsync();
        }
    }
}
