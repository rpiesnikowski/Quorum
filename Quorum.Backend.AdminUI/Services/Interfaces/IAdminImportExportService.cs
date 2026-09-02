using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services.Interfaces;

public interface IAdminImportExportService
{
    // Eksport poszczególnych encji do JSON
    Task<string> ExportClientsJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportApiScopesJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportIdentityResourcesJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportUsersJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportFederationsJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportGatewayRoutesJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportGrantsJsonAsync(CancellationToken cancellationToken = default);
    Task<string> ExportFullBackupJsonAsync(CancellationToken cancellationToken = default);

    // Import poszczególnych encji z JSON
    Task<DataImportResult> ImportClientsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportApiScopesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportIdentityResourcesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportUsersJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportFederationsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportGatewayRoutesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportGrantsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportFullBackupJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default);

    // Walidacja / wstępny podgląd przed wykonaniem importu
    DataImportPreview PreviewImportJson(string json, ImportEntityType targetType);
}
