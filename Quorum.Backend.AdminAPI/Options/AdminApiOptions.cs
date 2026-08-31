namespace Quorum.Backend.AdminAPI.Options;

/// <summary>
/// Opcje konfiguracyjne modułu Quorum Admin REST API.
/// </summary>
public class AdminApiOptions
{
    /// <summary>
    /// Prefiks ścieżki dla endpointów REST API (domyślnie "api/admin").
    /// </summary>
    public string RoutePrefix { get; set; } = "api/admin";

    /// <summary>
    /// Czy endpointy wymagają autoryzacji [Authorize] (domyślnie true).
    /// </summary>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>
    /// Nazwa polityki autoryzacji (opcjonalnie). Domyślnie null.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Wymagana rola użytkownika (opcjonalnie np. "Admin"). Domyślnie null.
    /// </summary>
    public string? RequiredRole { get; set; } = "Admin";

    /// <summary>
    /// Czy włączyć szczegółowe komunikaty błędów w odpowiedziach REST API.
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;
}
