namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Model wejściowy żądania testowego dla API Gateway.
/// </summary>
public class GatewayTestRequest
{
    /// <summary>
    /// Ścieżka wejściowa lub pełny adres URL (np. /api/v1/users/profile?details=true).
    /// </summary>
    public string RequestUrl { get; set; } = "/api/v1/users/profile";

    /// <summary>
    /// Metoda HTTP (GET, POST, PUT, DELETE, PATCH, OPTIONS, HEAD).
    /// </summary>
    public string HttpMethod { get; set; } = "GET";

    /// <summary>
    /// Nagłówki wejściowe (w formacie multiline "Klucz: Wartość").
    /// </summary>
    public string? RawHeaders { get; set; } = "Accept: application/json\nUser-Agent: Quorum-Gateway-Tester/1.0";

    /// <summary>
    /// Ciało żądania dla metod POST/PUT/PATCH.
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// Typ zawartości Content-Type ciała żądania.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Czy wykonać rzeczywiste żądanie HTTP do upstream serwera (Live Proxy), czy tylko dokonać ewaluacji reguły (Dry Run).
    /// </summary>
    public bool ExecuteLiveRequest { get; set; } = true;

    /// <summary>
    /// Ignorowanie błędów certyfikatu SSL/TLS (przydatne w środowiskach deweloperskich i mikrousługach wewnętrznych).
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = true;

    /// <summary>
    /// Opcjonalne nadpisanie limitu czasu (w sekundach).
    /// </summary>
    public int? CustomTimeoutSeconds { get; set; }
}

/// <summary>
/// Wynik ewaluacji pojedynczej trasy w kolejności priorytetów.
/// </summary>
public class GatewayRouteCandidateEvaluation
{
    public int RouteId { get; set; }
    public string? RouteName { get; set; }
    public string MatchPattern { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public string AllowedMethods { get; set; } = "ALL";
    public bool IsRegexMatch { get; set; }
    public bool IsMethodMatch { get; set; }
    public bool IsWinner { get; set; }
    public string EvaluationStatus { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Wynik etapu 1: Obliczenie i dopasowanie reguły routingu oraz transformacja adresu Upstream.
/// </summary>
public class GatewayEvaluationResult
{
    public bool IsMatched { get; set; }
    public string NormalizedPath { get; set; } = string.Empty;
    public string? OriginalQueryString { get; set; }
    public GatewayRoute? MatchedRoute { get; set; }
    public List<GatewayRouteCandidateEvaluation> CandidateEvaluations { get; set; } = new();

    /// <summary>
    /// Wyliczony pełny adres URL docelowego serwera (Upstream).
    /// </summary>
    public string CalculatedUpstreamUrl { get; set; } = string.Empty;

    /// <summary>
    /// Wszystkie nagłówki przygotowane do wysłania do serwera docelowego.
    /// </summary>
    public Dictionary<string, string> CalculatedHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Weryfikacja uwierzytelniania i uprawnień OIDC/JWT Scope.
    /// </summary>
    public bool AuthRequired { get; set; }
    public bool AuthPassed { get; set; }
    public string AuthStatusBadge { get; set; } = "badge bg-secondary";
    public string AuthSummary { get; set; } = string.Empty;
    public List<string> AuthDetails { get; set; } = new();
}

/// <summary>
/// Wynik etapu 2: Wykonanie rzeczywistego żądania HTTP do serwera docelowego i przechwycenie odpowiedzi.
/// </summary>
public class GatewayExecutionResult
{
    public bool Executed { get; set; }
    public int? StatusCode { get; set; }
    public string? StatusPhrase { get; set; }
    public bool IsSuccess { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ResponseBody { get; set; }
    public string? FormattedResponseBody { get; set; }
    public string? ResponseContentType { get; set; }
    public long? ContentLength { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Całościowy raport testu API Gateway (Ewaluacja + Odpowiedź HTTP).
/// </summary>
public class GatewayTestResponse
{
    public GatewayTestRequest Request { get; set; } = new();
    public GatewayEvaluationResult Evaluation { get; set; } = new();
    public GatewayExecutionResult Execution { get; set; } = new();
}
