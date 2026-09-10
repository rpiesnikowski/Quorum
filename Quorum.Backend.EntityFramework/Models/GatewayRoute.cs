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
    /// Szablon transformacji treści żądania (Body) przekazywanej do serwera docelowego (Upstream).
    /// Puste = przekazywanie treści bez zmian.
    /// (empty) = całkowite usunięcie treści żądania przed wysłaniem upstream.
    /// Szablon JSON Fluid: np. { "userId": {{ body.id }}, "targetEmail": "{{ body.email }}" }
    /// Szablon JSON JUST.net: np. { "userId": "#valueof($.id)", "targetEmail": "#valueof($.email)" }
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Silnik transformacji treści żądania: "Fluid" (Liquid - podwójne klamerki {{ body.JsonProperty }}) lub "JUST" (JUST.net JSON transformer).
    /// </summary>
    [MaxLength(32)]
    public string BodyTransformType { get; set; } = "Fluid";

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

    // --- Konfiguracja AuthZEN PEP (Policy Enforcement Point) ---

    /// <summary>
    /// Czy włączyć egzekwowanie polityki AuthZEN PEP dla tej trasy API Gateway.
    /// Jeśli true, żądanie zostanie przechwycone i wstrzymane w celu weryfikacji w PDP.
    /// </summary>
    public bool EnablePep { get; set; } = false;

    /// <summary>
    /// Niestandardowa nazwa akcji przekazywana do PDP (np. "can_read", "orders.create").
    /// Jeśli pusta, PEP użyje metody HTTP (GET, POST, etc.).
    /// </summary>
    [MaxLength(64)]
    public string? PepAction { get; set; }

    /// <summary>
    /// Typ zasobu przekazywany do PDP (domyślnie "route").
    /// </summary>
    [MaxLength(64)]
    public string? PepResourceType { get; set; } = "route";

    /// <summary>
    /// Niestandardowy identyfikator zasobu przekazywany do PDP (np. "orders-service", "billing-api").
    /// Jeśli pusty, PEP użyje ścieżki żądania (context.Request.Path).
    /// </summary>
    [MaxLength(255)]
    public string? PepResourceId { get; set; }

    /// <summary>
    /// Opcjonalny dedykowany adres URL silnika decyzyjnego PDP dla tej konkretnej trasy.
    /// Jeśli pusty, PEP użyje globalnie skonfigurowanego endpointu PDP z opcji bramki.
    /// </summary>
    [MaxLength(512)]
    public string? PepPdpEndpoint { get; set; }

    /// <summary>
    /// Data utworzenia rekordu
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data ostatniej modyfikacji
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
