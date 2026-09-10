using System.Text.Json.Serialization;

namespace Quorum.Backend.EntityFramework.AuthZen;

/// <summary>
/// Model podmiotu (Subject) w specyfikacji AuthZEN 1.0 (OpenID Foundation).
/// Reprezentuje użytkownika, serwis lub tożsamość żądającą dostępu.
/// </summary>
public class AuthZenSubject
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "user";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Properties { get; set; }
}

/// <summary>
/// Model akcji (Action) w specyfikacji AuthZEN 1.0.
/// Reprezentuje żądaną operację (np. metodę HTTP: GET, POST, DELETE lub logiczną: read, write, execute).
/// </summary>
public class AuthZenAction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "GET";

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Properties { get; set; }
}

/// <summary>
/// Model zasobu (Resource) w specyfikacji AuthZEN 1.0.
/// Reprezentuje chroniony obiekt lub endpoint API (np. ścieżkę /api/orders, dokument, transakcję).
/// </summary>
public class AuthZenResource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "route";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Properties { get; set; }
}

/// <summary>
/// Pojedyncze zapytanie ewaluacyjne PEP -> PDP (POST /access/v1/evaluation).
/// </summary>
public class AuthZenEvaluationRequest
{
    [JsonPropertyName("subject")]
    public AuthZenSubject Subject { get; set; } = new();

    [JsonPropertyName("action")]
    public AuthZenAction Action { get; set; } = new();

    [JsonPropertyName("resource")]
    public AuthZenResource Resource { get; set; } = new();

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Context { get; set; }
}

/// <summary>
/// Odpowiedź PDP z decyzją autoryzacyjną (AuthZEN 1.0 standard).
/// </summary>
public class AuthZenEvaluationResponse
{
    [JsonPropertyName("decision")]
    public bool Decision { get; set; }

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuthZenEvaluationResponseContext? Context { get; set; }
}

/// <summary>
/// Rozszerzone informacje kontekstowe i powód podjęcia decyzji przez PDP.
/// </summary>
public class AuthZenEvaluationResponseContext
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
    
    [JsonPropertyName("decision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Decision { get; set; }

    [JsonPropertyName("policy_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyId { get; set; }

    [JsonPropertyName("evaluator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Evaluator { get; set; } = "Quorum AuthZEN PDP Engine v1.0";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("attributes_evaluated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? AttributesEvaluated { get; set; }
}

/// <summary>
/// Zapytanie wsadowe ewaluacji (POST /access/v1/evaluations).
/// </summary>
public class AuthZenBatchEvaluationRequest
{
    [JsonPropertyName("evaluations")]
    public List<AuthZenEvaluationRequest> Evaluations { get; set; } = new();
}

/// <summary>
/// Odpowiedź wsadowa ewaluacji (POST /access/v1/evaluations).
/// </summary>
public class AuthZenBatchEvaluationResponse
{
    [JsonPropertyName("evaluations")]
    public List<AuthZenEvaluationResponse> Evaluations { get; set; } = new();
}

/// <summary>
/// Atrybuty tożsamości pobrane z PIP (Policy Information Point) - np. AspNetCoreIdentity.
/// </summary>
public class AuthZenSubjectAttributes
{
    public string SubjectId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLockedOut { get; set; }
    public List<string> Roles { get; set; } = new();
    public Dictionary<string, List<string>> Claims { get; set; } = new();
}
