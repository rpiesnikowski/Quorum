using Microsoft.JSInterop;
using Quorum.Backend.AdminUI.Services;
using Quorum.Backend.AdminUI.Components.Common;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Users;

public partial class UsersList : ComponentBase
{
    [Inject]
    public IAdminUserStore UserStore { get; set; } = default!;

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

    private RadzenDataGrid<UserAdminModel>? grid;
    private IEnumerable<UserAdminModel> users = new List<UserAdminModel>();
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
            var result = await UserStore.GetUsersAsync(searchTerm, 1, 1000);
            users = result.Items;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ToggleLockAsync(UserAdminModel user, bool lockAccount)
    {
        var result = await UserStore.ToggleLockoutAsync(user.Id, lockAccount);
        if (result.Success)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Sukces", lockAccount ? "Konto zostało zablokowane." : "Konto zostało odblokowane.");
            await LoadDataAsync();
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się zmienić statusu blokady.");
        }
    }

    private async Task ConfirmDeleteAsync(UserAdminModel user)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz bezpowrotnie usunąć użytkownika '{user.UserName}'?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Tak, usuń", CancelButtonText = "Anuluj" });
        if (confirmed == true)
        {
            var result = await UserStore.DeleteUserAsync(user.Id);
            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Użytkownik '{user.UserName}' został usunięty.");
                await LoadDataAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", result.Error ?? "Nie udało się usunąć użytkownika.");
            }
        }
    }

    private async Task ExportJsonAsync()
    {
        try
        {
            var json = await ImportExportService.ExportUsersJsonAsync();
            var fileName = $"quorum-users-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await FileDownloadHelper.DownloadJsonFileAsync(JSRuntime, fileName, json);
            NotificationService.Notify(NotificationSeverity.Success, "Eksport zakończony", $"Pomyślnie wyeksportowano użytkowników do pliku {fileName}");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd eksportu", ex.Message);
        }
    }

    private async Task OpenImportDialogAsync()
    {
        var result = await DialogService.OpenAsync<DataImportDialog>(
            "Import Użytkowników i Ról (JSON)",
            new Dictionary<string, object>
            {
                { "EntityType", ImportEntityType.Users }
            },
            new DialogOptions { Width = "750px", Resizable = true, Draggable = true });

        if (result is DataImportResult importResult && importResult.Success)
        {
            await LoadDataAsync();
        }
    }
}
