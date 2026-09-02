using System.Text.Json.Serialization;

namespace Quorum.Backend.AdminUI.Models;

public enum ImportStrategy
{
    /// <summary>
    /// Jeśli rekord o danym kluczu/nazwie istnieje, zaktualizuj go. Jeśli nie istnieje, utwórz nowy.
    /// </summary>
    Upsert = 0,

    /// <summary>
    /// Dodawaj tylko nowe rekordy. Jeśli rekord o danym kluczu/nazwie istnieje, pomiń go.
    /// </summary>
    AddNewOnly = 1,

    /// <summary>
    /// Usuń istniejące rekordy i zastąp je danymi ze źródła importu.
    /// </summary>
    ReplaceAll = 2
}

public enum ImportEntityType
{
    FullBackup = 0,
    Clients = 1,
    ApiScopes = 2,
    IdentityResources = 3,
    Users = 4,
    Federations = 5,
    GatewayRoutes = 6,
    Grants = 7
}

public class DataImportResult
{
    public bool Success { get; set; } = true;
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int DeletedCount { get; set; }
    public int TotalProcessed => AddedCount + UpdatedCount + SkippedCount;
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? SummaryMessage { get; set; }
}

public class DataImportPreview
{
    public bool IsValidJson { get; set; }
    public string? ErrorMessage { get; set; }
    public int DetectedCount { get; set; }
    public List<string> ItemIdentifiers { get; set; } = new();
    public ImportEntityType DetectedType { get; set; }
}

/// <summary>
/// Model pełnego zrzutu kopii zapasowej całej konfiguracji Quorum Backend.
/// Identyfikacja wszystkich powiązanych struktur odbywa się po nazwach / kluczach biznesowych.
/// </summary>
public class FullSystemBackupModel
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string System { get; set; } = "Quorum Identity & API Gateway";

    public List<ApiScopeAdminModel> ApiScopes { get; set; } = new();
    public List<IdentityResourceAdminModel> IdentityResources { get; set; } = new();
    public List<ClientAdminModel> Clients { get; set; } = new();
    public List<UserAdminModel> Users { get; set; } = new();
    public List<FederationAdminModel> Federations { get; set; } = new();
    public List<GatewayRouteAdminModel> GatewayRoutes { get; set; } = new();
    public List<PersistedGrantAdminModel> PersistedGrants { get; set; } = new();
}
