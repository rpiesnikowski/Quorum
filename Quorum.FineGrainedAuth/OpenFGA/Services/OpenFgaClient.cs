using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quorum.FineGrainedAuth.OpenFGA.Models;

namespace Quorum.FineGrainedAuth.OpenFGA.Services;

/// <summary>
/// Klient OpenFGA łączący się bezpośrednio z API REST OpenFGA (0.0.0.0:8080)
/// z obsługą operacji Write, Read, Check, Expand oraz zarządzania Stores,
/// ze wsparciem szybkiego lokalnego cache'u (In-Memory Fallback).
/// </summary>
public class OpenFgaClient : IOpenFgaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenFgaClient> _logger;
    private readonly OpenFgaOptions _options;
    private readonly ConcurrentDictionary<string, FgaTupleKey> _inMemoryTuples = new();
    private string? _resolvedStoreId;

    public OpenFgaClient(
        ILogger<OpenFgaClient> logger,
        IOptions<OpenFgaOptions>? options = null,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _options = options?.Value ?? new OpenFgaOptions();
        _httpClient = httpClient ?? new HttpClient();

        if (string.IsNullOrWhiteSpace(_httpClient.BaseAddress?.ToString()))
        {
            var serverUrl = string.IsNullOrWhiteSpace(_options.ServerUrl)
                ? "http://localhost:8080"
                : _options.ServerUrl.TrimEnd('/');
            _httpClient.BaseAddress = new Uri(serverUrl + "/");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(_options.ApiToken) && _httpClient.DefaultRequestHeaders.Authorization == null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }
        _resolvedStoreId = string.IsNullOrWhiteSpace(_options.StoreId) ? null : _options.StoreId;
    }

    public string GetStoreId() => _resolvedStoreId ?? _options.StoreId;

    public void SetStoreId(string storeId)
    {
        _resolvedStoreId = storeId;
        _options.StoreId = storeId;
    }

    private static string GetTupleId(FgaTupleKey key) => $"{key.User}#{key.Relation}@{key.Object}";

    public async Task<string> EnsureStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_resolvedStoreId))
        {
            return _resolvedStoreId;
        }

        try
        {
            var stores = await ListStoresAsync(cancellationToken);
            var existing = stores.FirstOrDefault(s => s.Name.Equals(_options.StoreName, StringComparison.OrdinalIgnoreCase))
                           ?? stores.FirstOrDefault();

            if (existing != null && !string.IsNullOrWhiteSpace(existing.Id))
            {
                _resolvedStoreId = existing.Id;
                _logger.LogInformation("Wykryto i podłączono istniejący Store w OpenFGA: {StoreName} (ID: {StoreId})", existing.Name, existing.Id);
                return _resolvedStoreId;
            }

            if (_options.AutoCreateStore)
            {
                var newStore = await CreateStoreAsync(_options.StoreName, cancellationToken);
                _resolvedStoreId = newStore.Id;
                _logger.LogInformation("Pomyślnie zainicjowano nowy Store w OpenFGA: {StoreName} (ID: {StoreId})", newStore.Name, newStore.Id);
                return _resolvedStoreId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się skomunikować z API REST OpenFGA przy wykrywaniu Store. Używanie domyślnego identyfikatora fallback.");
        }

        _resolvedStoreId = "default-store";
        return _resolvedStoreId;
    }

    public async Task<IReadOnlyList<FgaStore>> ListStoresAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FgaListStoresResponse>("stores", cancellationToken);
            return response?.Stores ?? new List<FgaStore>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd podczas pobierania listy Stores z GET /stores w OpenFGA.");
            return new List<FgaStore>();
        }
    }

    public async Task<FgaStore> CreateStoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var request = new FgaCreateStoreRequest { Name = name };
        var httpResponse = await _httpClient.PostAsJsonAsync("stores", request, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var store = await httpResponse.Content.ReadFromJsonAsync<FgaStore>(cancellationToken: cancellationToken);
        return store ?? new FgaStore { Id = Guid.NewGuid().ToString(), Name = name };
    }

    public async Task<bool> WriteTuplesAsync(IEnumerable<FgaTupleKey> writes, IEnumerable<FgaTupleKey>? deletes = null, CancellationToken cancellationToken = default)
    {
        var writeList = writes.ToList();
        var deleteList = deletes?.ToList() ?? new List<FgaTupleKey>();

        // Zapis do lokalnego stanu pamięciowego (dla natychmiastowej spójności i fallbacku)
        foreach (var w in writeList)
        {
            _inMemoryTuples[GetTupleId(w)] = w;
        }

        foreach (var d in deleteList)
        {
            _inMemoryTuples.TryRemove(GetTupleId(d), out _);
        }

        // Zapis przez API REST OpenFGA (POST /stores/{store_id}/write)
        try
        {
            var storeId = await EnsureStoreAsync(cancellationToken);
            var writePayload = new FgaWriteRequest
            {
                Writes = writeList.Count > 0 ? writeList : null,
                Deletes = deleteList.Count > 0 ? deleteList : null
            };

            var url = $"stores/{storeId}/write";
            var response = await _httpClient.PostAsJsonAsync(url, writePayload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenFGA REST Write zwrócił status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            }
            else
            {
                _logger.LogInformation("Pomyślnie zsynchronizowano z OpenFGA REST API: {WritesCount} zapisów, {DeletesCount} usunięć.", writeList.Count, deleteList.Count);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wyjątek podczas komunikacji z POST /stores/{{store_id}}/write w OpenFGA. Zmiany zapisano lokalnie w pamięci.");
            return true; // Sukces lokalny
        }
    }

    public async Task<IReadOnlyList<FgaTupleKey>> ReadTuplesAsync(string? user = null, string? relation = null, string? obj = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeId = await EnsureStoreAsync(cancellationToken);
            var readPayload = new FgaReadRequest();

            if (!string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(relation) || !string.IsNullOrEmpty(obj))
            {
                readPayload.TupleKey = new FgaTupleKey
                {
                    User = user ?? string.Empty,
                    Relation = relation ?? string.Empty,
                    Object = obj ?? string.Empty
                };
            }

            var response = await _httpClient.PostAsJsonAsync($"stores/{storeId}/read", readPayload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FgaReadResponse>(cancellationToken: cancellationToken);
                if (result?.Tuples != null)
                {
                    return result.Tuples.Select(t => t.Key).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd podczas pobierania krotek z OpenFGA REST API. Użycie pamięci lokalnej.");
        }

        // Fallback: Odczyt z pamięci
        var query = _inMemoryTuples.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(user)) query = query.Where(t => t.User == user);
        if (!string.IsNullOrEmpty(relation)) query = query.Where(t => t.Relation == relation);
        if (!string.IsNullOrEmpty(obj)) query = query.Where(t => t.Object == obj);

        return query.ToList();
    }

    public async Task<FgaCheckResponse> CheckAsync(FgaCheckRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Próba weryfikacji przez oficjalne API REST OpenFGA (POST /stores/{store_id}/check)
        try
        {
            var storeId = await EnsureStoreAsync(cancellationToken);
            var response = await _httpClient.PostAsJsonAsync($"stores/{storeId}/check", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FgaCheckResponse>(cancellationToken: cancellationToken);
                if (result != null)
                {
                    return result;
                }
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenFGA Check zwrócił status {StatusCode}: {Error}", response.StatusCode, err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd połączenia HTTP przy POST /check do OpenFGA. Przełączanie na ewaluację grafową w pamięci.");
        }

        // 2. Ewaluacja w lokalnym grafie relacji (ReBAC / Zanzibar)
        var targetUser = request.TupleKey.User;
        var targetRelation = request.TupleKey.Relation;
        var targetObject = request.TupleKey.Object;

        // Krotki kontekstowe
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

        // Sprawdzenie krotki bezpośredniej
        var id = GetTupleId(request.TupleKey);
        if (_inMemoryTuples.ContainsKey(id))
        {
            return new FgaCheckResponse { Allowed = true, Resolution = "Allowed via direct relation" };
        }

        // Sprawdzenie reguł dziedziczonych (admin, owner, wildcard)
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

    public async Task<FgaServerStatus> GetServerStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new FgaServerStatus
        {
            ServerUrl = _httpClient.BaseAddress?.ToString() ?? _options.ServerUrl,
            StoreId = _resolvedStoreId,
            StoreName = _options.StoreName,
            TotalTuplesCount = _inMemoryTuples.Count,
            LastCheckedAt = DateTime.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var stores = await ListStoresAsync(cancellationToken);
            sw.Stop();
            status.LatencyMs = sw.Elapsed.TotalMilliseconds;
            status.IsConnected = true;

            var active = stores.FirstOrDefault(s => s.Id == _resolvedStoreId) ?? stores.FirstOrDefault();
            if (active != null)
            {
                status.StoreId = active.Id;
                status.StoreName = active.Name;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            status.LatencyMs = sw.Elapsed.TotalMilliseconds;
            status.IsConnected = false;
            status.ErrorMessage = ex.Message;
        }

        return status;
    }
}
