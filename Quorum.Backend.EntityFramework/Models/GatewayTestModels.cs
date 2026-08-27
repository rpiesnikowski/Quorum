namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Model wejściowy żądania testowego dla API Gateway.
/// </summary>
public class GatewayTestRequest
{
    /// <summary>
    /// Ścieżka wejściowa lub pełny adres URL (np. /api/v1/orders/123?details=true).
    /// </summary>
    public string RequestUrl { get; set; } = "/api/v1/orders/123";

    /// <summary>
    /// Alias dla wstecznej kompatybilności.
    /// </summary>
    public string RequestPath
    {
        get => RequestUrl;
        set => RequestUrl = value;
    }

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
    /// Opcjonalny token Bearer JWT do wstrzyknięcia do nagłówka Authorization.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Zakresy (Scopes) przypisane do symulowanego tokenu JWT.
    /// </summary>
    public List<string> ProvidedScopes { get; set; } = new();

    /// <summary>
    /// Scopes w formie rozdzielonego spacjami ciągu tekstowego.
    /// </summary>
    public string ProvidedScopesText
    {
        get => string.Join(" ", ProvidedScopes);
        set => ProvidedScopes = (value ?? "")
            .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Distinct()
            .ToList();
    }

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
    public int? CustomTimeoutSeconds { get; set; } = 30;
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
    /// Niestandardowe nagłówki wstrzyknięte z konfiguracji trasy (route.Headers).
    /// </summary>
    public Dictionary<string, string> InjectedRouteHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Czy nagłówek Host klienta ma być przekazany dalej.
    /// </summary>
    public bool ForwardOriginalHost { get; set; }

    /// <summary>
    /// Weryfikacja uwierzytelniania i uprawnień OIDC/JWT Scope.
    /// </summary>
    public bool AuthRequired { get; set; }
    public bool AuthPassed { get; set; }
    public string AuthStatusBadge { get; set; } = "badge bg-secondary";
    public string AuthSummary { get; set; } = string.Empty;
    public List<string> MissingScopes { get; set; } = new();
    public List<string> AuthDetails { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
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

    /// <summary>
    /// Surowy zrzut wysłanego żądania HTTP (Raw HTTP Request).
    /// </summary>
    public string? RawRequest { get; set; }

    /// <summary>
    /// Surowy zrzut odebranej odpowiedzi HTTP (Raw HTTP Response).
    /// </summary>
    public string? RawResponse { get; set; }

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
public class GatewayTestResult
{
    public GatewayTestRequest Request { get; set; } = new();
    public GatewayEvaluationResult Evaluation { get; set; } = new();
    public GatewayExecutionResult Execution { get; set; } = new();

    // Właściwości pomocnicze kompatybilności wstecznej
    public bool IsMatch => Evaluation.IsMatched;
    public string TargetUri => Evaluation.CalculatedUpstreamUrl;
    public bool IsAuthorized => Evaluation.AuthPassed;
    public List<string> MissingScopes => Evaluation.MissingScopes;
    public string Explanation => !string.IsNullOrEmpty(Evaluation.Explanation) 
        ? Evaluation.Explanation 
        : (Evaluation.IsMatched ? "Trasa została dopasowana." : "Brak pasującej trasy Gateway.");
}

public class GatewayTestResponse : GatewayTestResult
{
}

