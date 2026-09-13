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
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "DefaultStore";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
