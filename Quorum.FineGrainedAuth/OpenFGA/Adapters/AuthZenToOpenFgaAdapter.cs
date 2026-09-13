using Microsoft.Extensions.Logging;
using Quorum.FineGrainedAuth.AuthZen.Models;
using Quorum.FineGrainedAuth.OpenFGA.Models;
using Quorum.FineGrainedAuth.OpenFGA.Services;

namespace Quorum.FineGrainedAuth.OpenFGA.Adapters;

/// <summary>
/// Mostek (Adapter) mapujący standardowe obiekty i polityki autoryzacji AuthZEN (OpenID Foundation)
/// na relacyjny model kontroli dostępu OpenFGA (Google Zanzibar ReBAC) i zapisujący je do API REST OpenFGA (POST /stores/{id}/write).
/// </summary>
public class AuthZenToOpenFgaAdapter
{
    private readonly IOpenFgaClient _fgaClient;
    private readonly ILogger<AuthZenToOpenFgaAdapter> _logger;

    public AuthZenToOpenFgaAdapter(IOpenFgaClient fgaClient, ILogger<AuthZenToOpenFgaAdapter> logger)
    {
        _fgaClient = fgaClient;
        _logger = logger;
    }

    /// <summary>
    /// Mapuje obiekt/regułę AuthZenRuleDto na krotkę relacji OpenFGA (user, relation, object).
    /// </summary>
    public static FgaTupleKey MapAuthZenRuleToTuple(AuthZenRuleDto rule)
    {
        var subjectUser = !string.IsNullOrWhiteSpace(rule.SubjectType) && !rule.SubjectId.Contains(':')
            ? $"{rule.SubjectType.ToLowerInvariant()}:{rule.SubjectId}"
            : rule.SubjectId;

        var relation = rule.Action.ToLowerInvariant().Trim().Replace(" ", "_");

        var targetObject = !string.IsNullOrWhiteSpace(rule.ResourceType) && !rule.ResourceId.Contains(':')
            ? $"{rule.ResourceType.ToLowerInvariant()}:{rule.ResourceId}"
            : rule.ResourceId;

        var tuple = new FgaTupleKey
        {
            User = subjectUser,
            Relation = relation,
            Object = targetObject
        };

        if (!string.IsNullOrWhiteSpace(rule.ConditionName))
        {
            tuple.Condition = new FgaRelationshipCondition
            {
                Name = rule.ConditionName
            };
        }

        return tuple;
    }

    /// <summary>
    /// Mapuje encję AuthZenPolicy na krotkę relacji OpenFGA.
    /// </summary>
    public static FgaTupleKey MapAuthZenPolicyToTuple(AuthZenPolicy policy)
    {
        var subjectUser = !string.IsNullOrWhiteSpace(policy.SubjectRoles)
            ? $"role:{policy.SubjectRoles.Split(',')[0].Trim().ToLowerInvariant()}"
            : $"{policy.SubjectType.ToLowerInvariant()}:{policy.Name.ToLowerInvariant()}";

        var action = policy.Action.ToUpperInvariant() switch
        {
            "GET" => "reader",
            "POST" or "PUT" => "writer",
            "DELETE" => "admin",
            "*" => "admin",
            _ => policy.Action.ToLowerInvariant().Replace(" ", "_")
        };

        var cleanResource = policy.ResourcePattern.TrimStart('/').Replace('/', '-').Replace('*', '_');
        var targetObject = $"{policy.ResourceType.ToLowerInvariant()}:{cleanResource}";

        return new FgaTupleKey
        {
            User = subjectUser,
            Relation = action,
            Object = targetObject
        };
    }

    /// <summary>
    /// Zapisuje regułę AuthZen do API REST OpenFGA za pomocą adaptera.
    /// Jeśli Effect == "Permit" i IsEnabled == true -> wykonuje zapis (Write) krotki w OpenFGA.
    /// Jeśli Effect == "Deny" lub IsEnabled == false -> usuwa (Delete) krotkę z OpenFGA.
    /// </summary>
    public async Task<AuthZenRuleSyncResult> SyncRuleToOpenFgaAsync(AuthZenRuleDto rule, CancellationToken cancellationToken = default)
    {
        var tuple = MapAuthZenRuleToTuple(rule);
        var storeId = _fgaClient.GetStoreId();

        try
        {
            bool isPermit = rule.IsEnabled && string.Equals(rule.Effect, "Permit", StringComparison.OrdinalIgnoreCase);

            if (isPermit)
            {
                _logger.LogInformation("Adapter AuthZen->OpenFGA: Zapisywanie krotki ({Tuple}) do API REST OpenFGA...", tuple);
                var success = await _fgaClient.WriteTuplesAsync(new[] { tuple }, null, cancellationToken);
                rule.SyncStatus = success ? "InSync" : "Error";
                rule.LastSyncedAt = DateTime.UtcNow;
                rule.LastSyncError = success ? null : "Błąd podczas zapisu w OpenFGA REST API.";

                return new AuthZenRuleSyncResult
                {
                    Success = success,
                    Message = success ? $"Pomyślnie utworzono i zapisano relację OpenFGA: {tuple}" : "Błąd zapisu w API OpenFGA",
                    SyncedCount = 1,
                    TupleKey = tuple,
                    StoreId = storeId
                };
            }
            else
            {
                _logger.LogInformation("Adapter AuthZen->OpenFGA: Usuwanie krotki ({Tuple}) z API REST OpenFGA (efekt Deny lub nieaktywna)...", tuple);
                var success = await _fgaClient.WriteTuplesAsync(Array.Empty<FgaTupleKey>(), new[] { tuple }, cancellationToken);
                rule.SyncStatus = "InSync";
                rule.LastSyncedAt = DateTime.UtcNow;

                return new AuthZenRuleSyncResult
                {
                    Success = success,
                    Message = $"Pomyślnie zaktualizowano stan w OpenFGA (usunięto relację z powodu {rule.Effect}): {tuple}",
                    SyncedCount = 1,
                    TupleKey = tuple,
                    StoreId = storeId
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd adaptera podczas synchronizacji reguły AuthZen '{RuleName}' do API REST OpenFGA.", rule.Name);
            rule.SyncStatus = "Error";
            rule.LastSyncError = ex.Message;

            return new AuthZenRuleSyncResult
            {
                Success = false,
                Message = $"Błąd zapisu w OpenFGA: {ex.Message}",
                TupleKey = tuple,
                StoreId = storeId
            };
        }
    }

    /// <summary>
    /// Usuwa powiązaną krotkę relacji z API REST OpenFGA po usunięciu reguły AuthZen.
    /// </summary>
    public async Task<AuthZenRuleSyncResult> DeleteRuleFromOpenFgaAsync(AuthZenRuleDto rule, CancellationToken cancellationToken = default)
    {
        var tuple = MapAuthZenRuleToTuple(rule);
        var storeId = _fgaClient.GetStoreId();

        try
        {
            _logger.LogInformation("Adapter AuthZen->OpenFGA: Usuwanie krotki {Tuple} z API OpenFGA...", tuple);
            var success = await _fgaClient.WriteTuplesAsync(Array.Empty<FgaTupleKey>(), new[] { tuple }, cancellationToken);

            return new AuthZenRuleSyncResult
            {
                Success = success,
                Message = $"Usunięto relację OpenFGA: {tuple}",
                SyncedCount = 1,
                TupleKey = tuple,
                StoreId = storeId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd adaptera podczas usuwania relacji {Tuple} z API REST OpenFGA.", tuple);
            return new AuthZenRuleSyncResult
            {
                Success = false,
                Message = $"Błąd usuwania z OpenFGA: {ex.Message}",
                TupleKey = tuple,
                StoreId = storeId
            };
        }
    }

    /// <summary>
    /// Hurtowa synchronizacja zestawu reguł AuthZen do OpenFGA.
    /// </summary>
    public async Task<AuthZenRuleSyncResult> SyncAllRulesAsync(IEnumerable<AuthZenRuleDto> rules, CancellationToken cancellationToken = default)
    {
        var ruleList = rules.ToList();
        var writes = new List<FgaTupleKey>();
        var deletes = new List<FgaTupleKey>();

        foreach (var r in ruleList)
        {
            var tuple = MapAuthZenRuleToTuple(r);
            if (r.IsEnabled && string.Equals(r.Effect, "Permit", StringComparison.OrdinalIgnoreCase))
            {
                writes.Add(tuple);
            }
            else
            {
                deletes.Add(tuple);
            }
        }

        try
        {
            var success = await _fgaClient.WriteTuplesAsync(writes, deletes, cancellationToken);
            foreach (var r in ruleList)
            {
                r.SyncStatus = success ? "InSync" : "Error";
                r.LastSyncedAt = DateTime.UtcNow;
            }

            return new AuthZenRuleSyncResult
            {
                Success = success,
                Message = $"Pomyślnie zsynchronizowano z OpenFGA REST API: {writes.Count} aktywnych relacji, {deletes.Count} usunięć.",
                SyncedCount = writes.Count,
                StoreId = _fgaClient.GetStoreId()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas hurtowej synchronizacji adaptera z OpenFGA.");
            return new AuthZenRuleSyncResult
            {
                Success = false,
                Message = $"Wyjątek synchronizacji hurtowej: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Mapuje AuthZenEvaluationRequest (Subject, Action, Resource) na zapytanie OpenFGA Tuple Check.
    /// </summary>
    public async Task<AuthZenEvaluationResponse> EvaluateViaFgaAsync(AuthZenEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var subjectUser = !request.Subject.Id.Contains(':')
            ? $"{request.Subject.Type}:{request.Subject.Id}"
            : request.Subject.Id;

        var actionRelation = request.Action.Name.ToLowerInvariant().Replace(" ", "_");

        var targetObject = !request.Resource.Id.Contains(':')
            ? $"{request.Resource.Type}:{request.Resource.Id}"
            : request.Resource.Id;

        var fgaRequest = new FgaCheckRequest
        {
            TupleKey = new FgaTupleKey
            {
                User = subjectUser,
                Relation = actionRelation,
                Object = targetObject
            },
            Context = request.Context
        };

        var fgaResult = await _fgaClient.CheckAsync(fgaRequest, cancellationToken);

        return new AuthZenEvaluationResponse
        {
            Decision = fgaResult.Allowed,
            Context = new AuthZenEvaluationResponseContext
            {
                Reason = fgaResult.Allowed 
                    ? $"Granted via OpenFGA Zanzibar relationship ({fgaResult.Resolution ?? "allowed"})." 
                    : $"Denied by OpenFGA: subject '{subjectUser}' does not have relation '{actionRelation}' on object '{targetObject}'.",
                Evaluator = "OpenFGA-Zanzibar-Adapter",
                Timestamp = DateTime.UtcNow,
                AttributesEvaluated = new Dictionary<string, object?>
                {
                    ["openfga.user"] = subjectUser,
                    ["openfga.relation"] = actionRelation,
                    ["openfga.object"] = targetObject,
                    ["openfga.allowed"] = fgaResult.Allowed.ToString()
                }
            }
        };
    }

    /// <summary>
    /// Zwraca bieżący stan serwera OpenFGA.
    /// </summary>
    public Task<FgaServerStatus> GetServerStatusAsync(CancellationToken cancellationToken = default)
    {
        return _fgaClient.GetServerStatusAsync(cancellationToken);
    }
}
