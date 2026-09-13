using System.Text.Json.Serialization;

namespace Quorum.FineGrainedAuth.OpenFGA.Models;

/// <summary>
/// Reprezentuje relację (Relationship Tuple) w modelu Fine-Grained Authorization (OpenFGA / Google Zanzibar).
/// Format: user (np. "user:anne"), relation (np. "reader", "owner"), object (np. "document:roadmap_2026").
/// </summary>
public class FgaTupleKey
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("relation")]
    public string Relation { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FgaRelationshipCondition? Condition { get; set; }

    public override string ToString() => $"({User}) is [{Relation}] of ({Object})";
}

/// <summary>
/// Warunek kontekstowy dla relacji w OpenFGA (ABAC + ReBAC).
/// </summary>
public class FgaRelationshipCondition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Context { get; set; }
}

/// <summary>
/// Żądanie weryfikacji uprawnień OpenFGA (POST /stores/{store_id}/check).
/// </summary>
public class FgaCheckRequest
{
    [JsonPropertyName("tuple_key")]
    public FgaTupleKey TupleKey { get; set; } = new();

    [JsonPropertyName("contextual_tuples")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FgaTupleKey>? ContextualTuples { get; set; }

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Context { get; set; }

    [JsonPropertyName("trace")]
    public bool Trace { get; set; } = false;
}

/// <summary>
/// Odpowiedź na zapytanie sprawdzające dostęp w OpenFGA.
/// </summary>
public class FgaCheckResponse
{
    [JsonPropertyName("allowed")]
    public bool Allowed { get; set; }

    [JsonPropertyName("resolution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Resolution { get; set; }
}

/// <summary>
/// Żądanie zapisu lub usunięcia relacji (POST /stores/{store_id}/write).
/// </summary>
public class FgaWriteRequest
{
    [JsonPropertyName("writes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FgaTupleKey>? Writes { get; set; }

    [JsonPropertyName("deletes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FgaTupleKey>? Deletes { get; set; }
}

/// <summary>
/// Odpowiedź z rozwinięciem drzewa relacji (Expand API).
/// </summary>
public class FgaExpandResponse
{
    [JsonPropertyName("tree")]
    public FgaNode? Tree { get; set; }
}

public class FgaNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("children")]
    public List<FgaNode> Children { get; set; } = new();
}

/// <summary>
/// Reprezentuje Store w OpenFGA.
/// </summary>
public class FgaStore
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "quorum-identity";

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public class FgaListStoresResponse
{
    [JsonPropertyName("stores")]
    public List<FgaStore> Stores { get; set; } = new();

    [JsonPropertyName("continuation_token")]
    public string? ContinuationToken { get; set; }
}

public class FgaCreateStoreRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "quorum-identity";
}

/// <summary>
/// Żądanie odczytu relacji (POST /stores/{store_id}/read).
/// </summary>
public class FgaReadRequest
{
    [JsonPropertyName("tuple_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FgaTupleKey? TupleKey { get; set; }

    [JsonPropertyName("page_size")]
    public int? PageSize { get; set; }

    [JsonPropertyName("continuation_token")]
    public string? ContinuationToken { get; set; }
}

public class FgaReadResponse
{
    [JsonPropertyName("tuples")]
    public List<FgaReadTupleItem> Tuples { get; set; } = new();

    [JsonPropertyName("continuation_token")]
    public string? ContinuationToken { get; set; }
}

public class FgaReadTupleItem
{
    [JsonPropertyName("key")]
    public FgaTupleKey Key { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Status połączenia i statystyki serwera OpenFGA.
/// </summary>
public class FgaServerStatus
{
    public string ServerUrl { get; set; } = "http://localhost:8080";
    public bool IsConnected { get; set; }
    public string? StoreId { get; set; }
    public string? StoreName { get; set; }
    public int TotalTuplesCount { get; set; }
    public double LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO dla reguły zdefiniowanej za pomocą AuthZEN i mapowanej na OpenFGA.
/// </summary>
public class AuthZenRuleDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Czytelna nazwa reguły (np. "Edycja dokumentów Roadmap przez Annę")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Opis biznesowy
    /// </summary>
    public string? Description { get; set; }

    // --- Składniki AuthZEN ---

    /// <summary>
    /// Typ podmiotu w AuthZEN: "user", "role", "group", "service"
    /// </summary>
    public string SubjectType { get; set; } = "user";

    /// <summary>
    /// Identyfikator podmiotu w AuthZEN: np. "anne", "admin", "finance"
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Akcja w AuthZEN / Relacja w OpenFGA (np. "reader", "writer", "owner", "admin", "GET", "POST")
    /// </summary>
    public string Action { get; set; } = "reader";

    /// <summary>
    /// Typ zasobu w AuthZEN: np. "document", "route", "repo", "order"
    /// </summary>
    public string ResourceType { get; set; } = "document";

    /// <summary>
    /// Identyfikator zasobu w AuthZEN: np. "roadmap_2026", "api/orders"
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Efekt w AuthZEN: "Permit" (zapis krotki relacji w OpenFGA) lub "Deny" (usunięcie krotki w OpenFGA)
    /// </summary>
    public string Effect { get; set; } = "Permit";

    /// <summary>
    /// Czy reguła jest aktywna
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Opcjonalny warunek OpenFGA / AuthZEN (np. "in_office_hours")
    /// </summary>
    public string? ConditionName { get; set; }

    // --- Właściwości wyliczeniowe dla OpenFGA Zanzibar ---

    /// <summary>
    /// Wygenerowany user dla OpenFGA: "{SubjectType}:{SubjectId}"
    /// </summary>
    public string FgaUser => $"{SubjectType.ToLowerInvariant()}:{SubjectId}";

    /// <summary>
    /// Wygenerowana relacja dla OpenFGA
    /// </summary>
    public string FgaRelation => Action.ToLowerInvariant().Replace(" ", "_");

    /// <summary>
    /// Wygenerowany object dla OpenFGA: "{ResourceType}:{ResourceId}"
    /// </summary>
    public string FgaObject => $"{ResourceType.ToLowerInvariant()}:{ResourceId}";

    /// <summary>
    /// Status synchronizacji z serwerem OpenFGA: "InSync", "Pending", "Error"
    /// </summary>
    public string SyncStatus { get; set; } = "Pending";

    /// <summary>
    /// Komunikat błędu synchronizacji (jeśli wystąpił)
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Data ostatniej synchronizacji z API REST OpenFGA
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Wynik synchronizacji pojedynczej lub wielu reguł do OpenFGA.
/// </summary>
public class AuthZenRuleSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int SyncedCount { get; set; }
    public FgaTupleKey? TupleKey { get; set; }
    public string? StoreId { get; set; }
}

