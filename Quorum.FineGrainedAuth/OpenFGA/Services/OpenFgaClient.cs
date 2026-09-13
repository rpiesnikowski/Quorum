using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Quorum.FineGrainedAuth.OpenFGA.Models;

namespace Quorum.FineGrainedAuth.OpenFGA.Services;

/// <summary>
/// Domyślna implementacja klienta OpenFGA z wbudowanym szybkim magazynem relacji grafowych (In-Memory Tuple Graph)
/// oraz opcjonalnym połączeniem HTTP z dedykowanym klastrem OpenFGA.
/// </summary>
public class OpenFgaClient : IOpenFgaClient
{
    private readonly HttpClient? _httpClient;
    private readonly ILogger<OpenFgaClient> _logger;
    private readonly ConcurrentDictionary<string, FgaTupleKey> _inMemoryTuples = new();

    public OpenFgaClient(ILogger<OpenFgaClient> logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    private static string GetTupleId(FgaTupleKey key) => $"{key.User}#{key.Relation}@{key.Object}";

    public async Task<FgaCheckResponse> CheckAsync(FgaCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (_httpClient != null && _httpClient.BaseAddress != null)
        {
            try
            {
                var httpResponse = await _httpClient.PostAsJsonAsync("check", request, cancellationToken);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var result = await httpResponse.Content.ReadFromJsonAsync<FgaCheckResponse>(cancellationToken: cancellationToken);
                    if (result != null) return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd komunikacji HTTP z instancją OpenFGA, przełączanie na ewaluację lokalną.");
            }
        }

        // Ewaluacja w lokalnym grafie relacji (ReBAC / Zanzibar)
        var targetUser = request.TupleKey.User;
        var targetRelation = request.TupleKey.Relation;
        var targetObject = request.TupleKey.Object;

        // Sprawdzenie krotek kontekstowych (przekazanych ad-hoc w żądaniu)
        if (request.ContextualTuples != null)
        {
            foreach (var ct in request.ContextualTuples)
            {
                if (Matches(ct, targetUser, targetRelation, targetObject))
                {
                    return new FgaCheckResponse { Allowed = true, Resolution = "Allowed via contextual tuple" };
                }
            }
        }

        // Sprawdzenie w magazynie lokalnym
        var id = GetTupleId(request.TupleKey);
        if (_inMemoryTuples.ContainsKey(id))
        {
            return new FgaCheckResponse { Allowed = true, Resolution = "Allowed via direct relation" };
        }

        // Sprawdzenie reguł uogólnionych (np. relacja 'admin' lub wildcard '*')
        foreach (var tuple in _inMemoryTuples.Values)
        {
            if (tuple.Object == targetObject && (tuple.User == "*" || tuple.User == targetUser))
            {
                if (tuple.Relation == targetRelation || tuple.Relation == "owner" || tuple.Relation == "admin")
                {
                    return new FgaCheckResponse { Allowed = true, Resolution = $"Allowed via inherited relation: {tuple.Relation}" };
                }
            }
        }

        return new FgaCheckResponse { Allowed = false, Resolution = "No matching relationship found" };
    }

    private static bool Matches(FgaTupleKey tuple, string user, string relation, string obj)
    {
        return (tuple.User == user || tuple.User == "*") &&
               (tuple.Relation == relation || tuple.Relation == "owner" || tuple.Relation == "admin") &&
               (tuple.Object == obj || tuple.Object == "*");
    }

    public Task<bool> WriteTuplesAsync(IEnumerable<FgaTupleKey> writes, IEnumerable<FgaTupleKey>? deletes = null, CancellationToken cancellationToken = default)
    {
        foreach (var w in writes)
        {
            _inMemoryTuples[GetTupleId(w)] = w;
        }

        if (deletes != null)
        {
            foreach (var d in deletes)
            {
                _inMemoryTuples.TryRemove(GetTupleId(d), out _);
            }
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<FgaTupleKey>> ReadTuplesAsync(string? user = null, string? relation = null, string? obj = null, CancellationToken cancellationToken = default)
    {
        var query = _inMemoryTuples.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(user))
            query = query.Where(t => t.User == user);
        if (!string.IsNullOrEmpty(relation))
            query = query.Where(t => t.Relation == relation);
        if (!string.IsNullOrEmpty(obj))
            query = query.Where(t => t.Object == obj);

        return Task.FromResult<IReadOnlyList<FgaTupleKey>>(query.ToList());
    }

    public Task<FgaExpandResponse> ExpandAsync(string relation, string obj, CancellationToken cancellationToken = default)
    {
        var matching = _inMemoryTuples.Values
            .Where(t => t.Relation == relation && t.Object == obj)
            .Select(t => new FgaNode { Name = t.User })
            .ToList();

        return Task.FromResult(new FgaExpandResponse
        {
            Tree = new FgaNode
            {
                Name = $"{relation}@{obj}",
                Children = matching
            }
        });
    }
}
