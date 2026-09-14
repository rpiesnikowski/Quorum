namespace Quorum.FineGrainedAuth.OpenFGA.Models;

/// <summary>
/// Opcje konfiguracyjne dla klienta OpenFGA REST API.
/// </summary>
public class OpenFgaOptions
{
    public const string SectionName = "OpenFga";

    /// <summary>
    /// Adres bazowy serwera HTTP OpenFGA (domyślnie http://localhost:8080).
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Alias dla ServerUrl dla wygody konfiguracji.
    /// </summary>
    public string ApiUrl
    {
        get => ServerUrl;
        set => ServerUrl = value;
    }

    /// <summary>
    /// Identyfikator magazynu (Store ID). Jeśli puste, klient pobierze pierwszy dostępny store lub utworzy nowy.
    /// </summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Nazwa magazynu do utworzenia/użycia w OpenFGA (domyślnie "quorum-identity").
    /// </summary>
    public string StoreName { get; set; } = "quorum-identity";

    /// <summary>
    /// Czy automatycznie utworzyć Store w OpenFGA, jeśli żaden nie istnieje.
    /// </summary>
    public bool AutoCreateStore { get; set; } = true;

    /// <summary>
    /// Opcjonalny identyfikator zarejestrowanego modelu autoryzacyjnego.
    /// </summary>
    public string? AuthorizationModelId { get; set; }

    /// <summary>
    /// Alias dla AuthorizationModelId dla spójności konfiguracji.
    /// </summary>
    public string? DefaultAuthorizationModelId
    {
        get => AuthorizationModelId;
        set => AuthorizationModelId = value;
    }

    /// <summary>
    /// Opcjonalny token API Bearer / Preshared Key dla zabezpieczonych instancji OpenFGA.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>
    /// Limit czasu żądania HTTP do OpenFGA (w sekundach).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
