using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Open.IdentityServer.EntityFramework.Entities;
namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Reprezentuje regułę routingu / reverse proxy w API Gateway.
/// </summary>
[Table("GatewayRoutes")]
public class GatewayRoute
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Wzorzec dopasowania ścieżki Regex (np. ^/api/v1/users/.* lub ^/api/orders(?<rest>/.*)?)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string MatchPattern { get; set; } = string.Empty;

    /// <summary>
    /// Czytelna nazwa trasy (np. Users Microservice, Billing API)
    /// </summary>
    [MaxLength(128)]
    public string? RouteName { get; set; }

    /// <summary>
    /// Opis funkcjonalny reguły routingu
    /// </summary>
    [MaxLength(512)]
    public string? Description { get; set; }

    // --- Segmenty URI dla Reverse Proxy ---

    /// <summary>
    /// Docelowy schemat protokołu (http, https)
    /// </summary>
    [MaxLength(16)]
    public string Scheme { get; set; } = "https";

    /// <summary>
    /// Host docelowy (np. users-service.internal, api.example.com, 10.0.0.5)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AddressHost { get; set; } = string.Empty;

    /// <summary>
    /// Port docelowy (np. 443, 8080, 5000)
    /// </summary>
    public int AddressPort { get; set; } = 443;

    /// <summary>
    /// Ścieżka bazowa na serwerze docelowym (np. /v1, /api)
    /// </summary>
    [MaxLength(255)]
    public string? AddressBasePath { get; set; }

    /// <summary>
    /// Ścieżka docelowa lub szablon podstawienia (opcjonalny override ścieżki)
    /// </summary>
    [MaxLength(255)]
    public string? AddressPath { get; set; }

    /// <summary>
    /// Domyślne parametry query string doklejane do zapytania upstream
    /// </summary>
    [MaxLength(500)]
    public string? AddressQueryString { get; set; }

    /// <summary>
    /// Dodatkowe lub modyfikowane nagłówki HTTP przekazywane do serwisu docelowego (JSON lub klucz=wartość)
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Limit czasu oczekiwania w sekundach (Timeout)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Dozwolone metody HTTP (np. GET,POST,PUT,DELETE lub ALL)
    /// </summary>
    [MaxLength(64)]
    public string HttpMethods { get; set; } = "ALL";

    // --- Zabezpieczenia, Uwierzytelnianie & Uprawnienia ---

    /// <summary>
    /// Czy ruch do tej trasy jest dozwolony anonimowo (bez walidacji tokenu JWT)
    /// </summary>
    public bool AllowAnonymous { get; set; } = false;

    /// <summary>
    /// Czy wymagana jest weryfikacja konkretnych Scope (RequiredScope)
    /// </summary>
    public bool RequiredScope { get; set; } = false;

    /// <summary>
    /// Kolekcja przypisanych zakresów (Scopes) powiązanych z tą trasą API Gateway
    /// </summary>
    public virtual ICollection<GatewayRouteScope> Scopes { get; set; } = new List<GatewayRouteScope>();

    /// <summary>
    /// Klucz obcy do tabeli ApiScopes (opcjonalny, zachowany dla kompatybilności)
    /// </summary>
    public int? ApiScopeId { get; set; }

    /// <summary>
    /// Relacja nawigacyjna do encji ApiScope z Open.IdentityServer.EntityFramework
    /// </summary>
    [ForeignKey(nameof(ApiScopeId))]
    public virtual ApiScope? ApiScope { get; set; }

    /// <summary>
    /// Zapasowy / zagregowany tekst nazw zakresów (np. "api1 api.read")
    /// </summary>
    [MaxLength(500)]
    public string? ScopeName { get; set; }

    /// <summary>
    /// Wymagane schematy uwierzytelniania dla routingu (np. Bearer, Cookies, OpenIdConnect, entra-id)
    /// </summary>
    [MaxLength(255)]
    public string? AuthenticationSchemes { get; set; } = "Bearer";

    /// <summary>
    /// Czy reguła jest aktywna
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Priorytet dopasowania (wyższy numer = sprawdzany wcześniej)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Czy włączać buforowanie odpowiedzi (Response Caching)
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Czy przekazywać nagłówki X-Forwarded-* (Proto, For, Host)
    /// </summary>
    public bool ForwardOriginalHost { get; set; } = true;

    /// <summary>
    /// Data utworzenia rekordu
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data ostatniej modyfikacji
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
