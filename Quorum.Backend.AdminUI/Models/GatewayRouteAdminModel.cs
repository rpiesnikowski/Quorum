using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Models;

/// <summary>
/// Model administracyjny dla reguły routingu API Gateway (tabela GatewayRoutes).
/// </summary>
public class GatewayRouteAdminModel
{
    public int Id { get; set; }

    // --- 1. Podstawowe dopasowanie trasy (Inbound & Match Pattern) ---

    /// <summary>
    /// Wzorzec dopasowania ścieżki (np. /api/orders, ^/api/v1/users/.*)
    /// </summary>
    [Required(ErrorMessage = "Wzorzec dopasowania ścieżki (MatchPattern) jest wymagany.")]
    [StringLength(255, ErrorMessage = "Wzorzec ścieżki nie może przekraczać 255 znaków.")]
    public string MatchPattern { get; set; } = "/api/";

    /// <summary>
    /// Alias dla wstecznej kompatybilności
    /// </summary>
    public string PathPattern
    {
        get => MatchPattern;
        set => MatchPattern = value;
    }

    /// <summary>
    /// Czytelna nazwa trasy (np. Orders Microservice, Billing API)
    /// </summary>
    [MaxLength(128, ErrorMessage = "Nazwa trasy nie może przekraczać 128 znaków.")]
    public string? RouteName { get; set; }

    /// <summary>
    /// Alias Name dla wstecznej kompatybilności
    /// </summary>
    public string Name
    {
        get => RouteName ?? MatchPattern;
        set => RouteName = value;
    }

    /// <summary>
    /// Opis funkcjonalny trasy
    /// </summary>
    [MaxLength(512, ErrorMessage = "Opis nie może przekraczać 512 znaków.")]
    public string? Description { get; set; }

    /// <summary>
    /// Priorytet dopasowania (wyższy numer = reguła sprawdzana wcześniej)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Czy trasa jest włączona w potoku proxy
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Dozwolone metody HTTP (np. GET, POST, PUT, DELETE lub ALL)
    /// </summary>
    public List<string> AllowedHttpMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

    public string HttpMethods
    {
        get => AllowedHttpMethods != null && AllowedHttpMethods.Count > 0 ? string.Join(",", AllowedHttpMethods) : "ALL";
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                AllowedHttpMethods = new() { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
            }
            else
            {
                AllowedHttpMethods = value
                    .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim().ToUpperInvariant())
                    .Distinct()
                    .ToList();
            }
        }
    }

    public bool IsRegex => !string.IsNullOrEmpty(MatchPattern) && (MatchPattern.StartsWith("^") || MatchPattern.Contains(".*"));

    // --- 2. Cel przekierowania (Upstream URI & Target Host) ---

    /// <summary>
    /// Protokół docelowy (http lub https)
    /// </summary>
    [Required(ErrorMessage = "Protokół jest wymagany.")]
    [MaxLength(16)]
    public string Scheme { get; set; } = "https";

    /// <summary>
    /// Host docelowy (np. orders-service.internal, api.example.com, localhost)
    /// </summary>
    [Required(ErrorMessage = "Host docelowy (AddressHost) jest wymagany.")]
    [MaxLength(255, ErrorMessage = "Host nie może przekraczać 255 znaków.")]
    public string AddressHost { get; set; } = "localhost";

    /// <summary>
    /// Port serwera docelowego (np. 443, 80, 5001, 8080)
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port musi mieścić się w zakresie 1-65535.")]
    public int AddressPort { get; set; } = 443;

    /// <summary>
    /// Ścieżka bazowa na serwerze docelowym (np. /v1, /api)
    /// </summary>
    [MaxLength(255)]
    public string? AddressBasePath { get; set; }

    /// <summary>
    /// Opcjonalne nadpisanie ścieżki docelowej (np. /api/v2/orders)
    /// </summary>
    [MaxLength(255)]
    public string? AddressPath { get; set; }

    public string? DownstreamPath
    {
        get => AddressPath;
        set => AddressPath = value;
    }

    /// <summary>
    /// Domyślne parametry query string doklejane do zapytania upstream
    /// </summary>
    [MaxLength(500)]
    public string? AddressQueryString { get; set; }

    /// <summary>
    /// Pełny adres URL Upstream z automatycznym parsowaniem
    /// </summary>
    public string UpstreamHost
    {
        get
        {
            var portStr = (AddressPort == 80 && Scheme == "http") || (AddressPort == 443 && Scheme == "https") ? "" : $":{AddressPort}";
            var baseP = string.IsNullOrWhiteSpace(AddressBasePath) ? "" : (AddressBasePath.StartsWith("/") ? AddressBasePath : "/" + AddressBasePath);
            return $"{Scheme}://{AddressHost}{portStr}{baseP}";
        }
        set
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                Scheme = uri.Scheme;
                AddressHost = uri.Host;
                AddressPort = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);
                AddressBasePath = uri.AbsolutePath.TrimEnd('/');
            }
        }
    }

    // --- 3. Autoryzacja, Uwierzytelnianie & Scopes ---

    /// <summary>
    /// Czy ruch do tej trasy jest dozwolony anonimowo (bez walidacji tokenu JWT)
    /// </summary>
    public bool AllowAnonymous { get; set; } = false;

    /// <summary>
    /// Czy wymagana jest weryfikacja konkretnych Scope (RequiredScope)
    /// </summary>
    public bool RequiredScope { get; set; } = false;

    /// <summary>
    /// Lista wymaganych zakresów (Scopes)
    /// </summary>
    public List<string> RequiredScopes { get; set; } = new();

    /// <summary>
    /// Klucz obcy do tabeli ApiScopes (opcjonalny)
    /// </summary>
    public int? ApiScopeId { get; set; }

    /// <summary>
    /// Zapasowy / zagregowany tekst nazw zakresów
    /// </summary>
    [MaxLength(500)]
    public string? ScopeName
    {
        get => RequiredScopes != null && RequiredScopes.Count > 0 ? string.Join(" ", RequiredScopes) : null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                RequiredScopes = value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            }
        }
    }

    /// <summary>
    /// Wymagane schematy uwierzytelniania dla routingu (np. Bearer, Cookies)
    /// </summary>
    [MaxLength(255)]
    public string? AuthenticationSchemes { get; set; } = "Bearer";

    // --- 4. Zaawansowane Parametry Proxy & Wydajność ---

    /// <summary>
    /// Limit czasu oczekiwania na odpowiedź w sekundach (Timeout)
    /// </summary>
    [Range(1, 600, ErrorMessage = "Timeout musi mieścić się w zakresie 1-600 sekund.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Czy przekazywać oryginalny nagłówek Host z żądania klienta
    /// </summary>
    public bool ForwardOriginalHost { get; set; } = true;

    /// <summary>
    /// Czy włączać buforowanie odpowiedzi (Response Caching)
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Dodatkowe lub modyfikowane nagłówki HTTP przekazywane do serwisu docelowego (JSON lub linie klucz=wartość)
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Data utworzenia rekordu
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data ostatniej modyfikacji
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // --- Podsumowania UI ---
    public string ScopesSummary => AllowAnonymous 
        ? "Anonimowy (Publiczny)" 
        : (RequiredScopes != null && RequiredScopes.Count > 0 ? string.Join(", ", RequiredScopes) : "Wymagany Token JWT (Bez Scopes)");
    
    public string MethodsSummary => AllowedHttpMethods != null && AllowedHttpMethods.Count > 0 ? string.Join(", ", AllowedHttpMethods) : "Wszystkie";
    public string StatusSummary => IsEnabled ? "Aktywna" : "Wyłączona";

    public string AllowedHttpMethodsText
    {
        get => string.Join(", ", AllowedHttpMethods);
        set => AllowedHttpMethods = (value ?? "")
            .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public string RequiredScopesText
    {
        get => string.Join(", ", RequiredScopes);
        set => RequiredScopes = (value ?? "")
            .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Distinct()
            .ToList();
    }
}
