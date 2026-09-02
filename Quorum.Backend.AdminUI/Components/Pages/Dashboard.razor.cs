using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Quorum.Backend.AdminUI.Components.Common;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages;

public partial class Dashboard : ComponentBase
{
    [Inject]
    public IAdminDashboardStore DashboardStore { get; set; } = default!;

    [Inject]
    public IAdminImportExportService ImportExportService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private DashboardStatsModel? stats;

    protected override async Task OnInitializedAsync()
    {
        await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        stats = await DashboardStore.GetStatsAsync();
    }

    private async Task ExportFullBackupAsync()
    {
        try
        {
            var json = await ImportExportService.ExportFullBackupJsonAsync();
            var fileName = $"quorum-full-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await FileDownloadHelper.DownloadJsonFileAsync(JSRuntime, fileName, json);
            NotificationService.Notify(NotificationSeverity.Success, "Eksport zakończony", $"Pomyślnie wyeksportowano pełną kopię systemu do pliku {fileName}");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd eksportu", ex.Message);
        }
    }

    private async Task OpenFullImportDialogAsync()
    {
        var result = await DialogService.OpenAsync<DataImportDialog>(
            "Import Pełnej Kopii Zapasowej Systemu (JSON)",
            new Dictionary<string, object>
            {
                { "EntityType", ImportEntityType.FullBackup }
            },
            new DialogOptions { Width = "800px", Resizable = true, Draggable = true });

        if (result is DataImportResult importResult && importResult.Success)
        {
            await LoadStatsAsync();
        }
    }
}
