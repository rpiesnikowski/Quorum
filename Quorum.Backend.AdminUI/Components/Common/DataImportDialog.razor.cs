using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Common;

public partial class DataImportDialog : ComponentBase
{
    [Parameter]
    public ImportEntityType EntityType { get; set; } = ImportEntityType.Clients;

    [Parameter]
    public string? CustomTitle { get; set; }

    [Inject]
    public IAdminImportExportService ImportExportService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private ImportStrategy selectedStrategy = ImportStrategy.Upsert;
    private string jsonContent = string.Empty;
    private string? fileName;
    private long fileSizeKb;
    private bool isProcessing;
    private DataImportPreview preview = new();

    protected override void OnInitialized()
    {
        ValidateAndPreview();
    }

    private string GetTitle()
    {
        if (!string.IsNullOrEmpty(CustomTitle)) return CustomTitle;

        return EntityType switch
        {
            ImportEntityType.Clients => "Import Klientów OAuth / OIDC",
            ImportEntityType.ApiScopes => "Import Zakresów API (ApiScopes)",
            ImportEntityType.IdentityResources => "Import Zasobów Tożsamości",
            ImportEntityType.Users => "Import Użytkowników i Ról",
            ImportEntityType.Federations => "Import Dostawców Federacji (SSO)",
            ImportEntityType.GatewayRoutes => "Import Tras API Gateway",
            ImportEntityType.Grants => "Import Aktywnych Grantów / Tokenów",
            ImportEntityType.FullBackup => "Przywracanie Pełnej Kopii Zapasowej Środowiska",
            _ => "Import Danych"
        };
    }

    private string GetDescription()
    {
        return EntityType switch
        {
            ImportEntityType.Clients => "Wczytaj definicje klientów, zakresy uprawnień, URI przekierowań i poświadczenia.",
            ImportEntityType.ApiScopes => "Wczytaj definicje zakresów API oraz przypisane roszczenia (User Claims).",
            ImportEntityType.IdentityResources => "Wczytaj standardowe i niestandardowe zasoby tożsamości OIDC.",
            ImportEntityType.Users => "Wczytaj konta użytkowników, przypisane role oraz powiązane roszczenia.",
            ImportEntityType.Federations => "Wczytaj konfiguracje zewnętrznych dostawców logowania OpenID Connect.",
            ImportEntityType.GatewayRoutes => "Wczytaj reguły przekazywania żądań (routing, nagłówki, autoryzacja).",
            ImportEntityType.Grants => "Wczytaj stan persystowanych grantów i tokenów autoryzacyjnych.",
            ImportEntityType.FullBackup => "Odtwórz kompletne środowisko Quorum ze wszystkimi modułami i relacjami.",
            _ => "Wczytaj konfigurację z pliku JSON."
        };
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            if (file != null)
            {
                fileName = file.Name;
                fileSizeKb = Math.Max(1, file.Size / 1024);

                using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // max 10MB
                using var reader = new StreamReader(stream);
                jsonContent = await reader.ReadToEndAsync();
                ValidateAndPreview();
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd odczytu pliku", ex.Message);
        }
    }

    private void OnJsonInput(ChangeEventArgs e)
    {
        jsonContent = e.Value?.ToString() ?? string.Empty;
        ValidateAndPreview();
    }

    private void ValidateAndPreview()
    {
        preview = ImportExportService.PreviewImportJson(jsonContent, EntityType);
    }

    private async Task ExecuteImportAsync()
    {
        if (string.IsNullOrWhiteSpace(jsonContent) || !preview.IsValidJson || preview.DetectedCount == 0)
        {
            return;
        }

        isProcessing = true;
        try
        {
            DataImportResult result = EntityType switch
            {
                ImportEntityType.Clients => await ImportExportService.ImportClientsJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.ApiScopes => await ImportExportService.ImportApiScopesJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.IdentityResources => await ImportExportService.ImportIdentityResourcesJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.Users => await ImportExportService.ImportUsersJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.Federations => await ImportExportService.ImportFederationsJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.GatewayRoutes => await ImportExportService.ImportGatewayRoutesJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.Grants => await ImportExportService.ImportGrantsJsonAsync(jsonContent, selectedStrategy),
                ImportEntityType.FullBackup => await ImportExportService.ImportFullBackupJsonAsync(jsonContent, selectedStrategy),
                _ => new DataImportResult { Success = false, Errors = { "Nieobsługiwany typ importu." } }
            };

            if (result.Success)
            {
                NotificationService.Notify(
                    NotificationSeverity.Success, 
                    "Import zakończony sukcesem", 
                    result.SummaryMessage ?? $"Pomyślnie przetworzono {result.TotalProcessed} pozycji.");

                DialogService.Close(result);
            }
            else
            {
                var errorSummary = result.Errors.Count > 0 ? string.Join("\n", result.Errors) : "Wystąpił błąd podczas importu danych.";
                NotificationService.Notify(NotificationSeverity.Error, "Błąd importu", errorSummary, duration: 6000);
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd importu", ex.Message, duration: 6000);
        }
        finally
        {
            isProcessing = false;
        }
    }
}
