using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quorum.FineGrainedAuth.OpenFGA.Adapters;
using Quorum.FineGrainedAuth.OpenFGA.Models;

namespace Quorum.FineGrainedAuth.OpenFGA.Services;

/// <summary>
/// Zarządza regułami autoryzacyjnymi zdefiniowanymi w standardzie AuthZEN
/// oraz wykorzystuje AuthZenToOpenFgaAdapter do natychmiastowego zapisu do API REST OpenFGA (0.0.0.0:8080).
/// </summary>
public class AuthZenOpenFgaStore : IAuthZenOpenFgaStore
{
    private readonly ConcurrentDictionary<string, AuthZenRuleDto> _rules = new();
    private readonly AuthZenToOpenFgaAdapter _adapter;
    private readonly ILogger<AuthZenOpenFgaStore> _logger;

    public AuthZenOpenFgaStore(AuthZenToOpenFgaAdapter adapter, ILogger<AuthZenOpenFgaStore> logger)
    {
        _adapter = adapter;
        _logger = logger;
        SeedInitialRules();
    }

    private void SeedInitialRules()
    {
        var initial = new[]
        {
            new AuthZenRuleDto
            {
                Id = "rule-001",
                Name = "Odczyt Roadmap przez Annę",
                Description = "Użytkownik Anne posiada uprawnienie odczytu (reader) dokumentu Roadmap 2026",
                SubjectType = "user",
                SubjectId = "anne",
                Action = "reader",
                ResourceType = "document",
                ResourceId = "roadmap_2026",
                Effect = "Permit",
                IsEnabled = true,
                SyncStatus = "Pending"
            },
            new AuthZenRuleDto
            {
                Id = "rule-002",
                Name = "Uprawnienia Właściciela dla Administratora",
                Description = "Rola Admin posiada pełne uprawnienia właściciela (owner) repozytorium Quorum",
                SubjectType = "role",
                SubjectId = "admin",
                Action = "owner",
                ResourceType = "repo",
                ResourceId = "quorum-core",
                Effect = "Permit",
                IsEnabled = true,
                SyncStatus = "Pending"
            },
            new AuthZenRuleDto
            {
                Id = "rule-003",
                Name = "Zapis Zamówień przez Dział Finansowy",
                Description = "Grupa Finanse posiada uprawnienia zapisu (writer) w module faktur",
                SubjectType = "group",
                SubjectId = "finance",
                Action = "writer",
                ResourceType = "route",
                ResourceId = "api-orders",
                Effect = "Permit",
                IsEnabled = true,
                SyncStatus = "Pending"
            },
            new AuthZenRuleDto
            {
                Id = "rule-004",
                Name = "Dostęp Audytora Bezpieczeństwa",
                Description = "Audytor posiada wgląd do logów telemetrycznych systemu",
                SubjectType = "user",
                SubjectId = "security_auditor",
                Action = "viewer",
                ResourceType = "api",
                ResourceId = "audit_telemetry",
                Effect = "Permit",
                IsEnabled = true,
                SyncStatus = "Pending"
            }
        };

        foreach (var r in initial)
        {
            _rules[r.Id] = r;
        }
    }

    public Task<IReadOnlyList<AuthZenRuleDto>> GetRulesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _rules.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(r => 
                r.Name.ToLowerInvariant().Contains(s) ||
                r.SubjectId.ToLowerInvariant().Contains(s) ||
                r.Action.ToLowerInvariant().Contains(s) ||
                r.ResourceId.ToLowerInvariant().Contains(s) ||
                r.FgaUser.ToLowerInvariant().Contains(s) ||
                r.FgaObject.ToLowerInvariant().Contains(s));
        }

        var list = query.OrderByDescending(r => r.LastSyncedAt).ToList();
        return Task.FromResult<IReadOnlyList<AuthZenRuleDto>>(list);
    }

    public Task<AuthZenRuleDto?> GetRuleByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(id, out var rule);
        return Task.FromResult(rule);
    }

    public async Task<AuthZenRuleSyncResult> CreateRuleAsync(AuthZenRuleDto rule, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            rule.Id = Guid.NewGuid().ToString("N")[..8];
        }

        _rules[rule.Id] = rule;
        _logger.LogInformation("Utworzono regułę AuthZen '{RuleName}' (ID: {RuleId}). Rozpoczynanie synchronizacji przez adapter do OpenFGA...", rule.Name, rule.Id);

        // Zapis do API REST OpenFGA za pomocą adaptera
        var syncResult = await _adapter.SyncRuleToOpenFgaAsync(rule, cancellationToken);
        return syncResult;
    }

    public async Task<AuthZenRuleSyncResult> UpdateRuleAsync(string id, AuthZenRuleDto rule, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(id, out var existing);
        rule.Id = id;

        // Jeśli zmieniły się klucze relacji (Subject, Action, Object), usuwamy starą krotkę
        if (existing != null && (existing.FgaUser != rule.FgaUser || existing.FgaRelation != rule.FgaRelation || existing.FgaObject != rule.FgaObject))
        {
            await _adapter.DeleteRuleFromOpenFgaAsync(existing, cancellationToken);
        }

        _rules[id] = rule;
        var syncResult = await _adapter.SyncRuleToOpenFgaAsync(rule, cancellationToken);
        return syncResult;
    }

    public async Task<AuthZenRuleSyncResult> DeleteRuleAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_rules.TryRemove(id, out var rule))
        {
            var syncResult = await _adapter.DeleteRuleFromOpenFgaAsync(rule, cancellationToken);
            return syncResult;
        }

        return new AuthZenRuleSyncResult
        {
            Success = false,
            Message = $"Nie znaleziono reguły o ID {id}."
        };
    }

    public async Task<AuthZenRuleSyncResult> SyncRuleAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_rules.TryGetValue(id, out var rule))
        {
            return await _adapter.SyncRuleToOpenFgaAsync(rule, cancellationToken);
        }

        return new AuthZenRuleSyncResult
        {
            Success = false,
            Message = $"Nie znaleziono reguły o ID {id}."
        };
    }

    public async Task<AuthZenRuleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var rules = _rules.Values.ToList();
        return await _adapter.SyncAllRulesAsync(rules, cancellationToken);
    }

    public async Task<FgaCheckResponse> CheckAccessAsync(string user, string relation, string obj, CancellationToken cancellationToken = default)
    {
        var request = new FgaCheckRequest
        {
            TupleKey = new FgaTupleKey
            {
                User = user,
                Relation = relation,
                Object = obj
            }
        };

        return await _adapter.EvaluateViaFgaAsync(new AuthZen.Models.AuthZenEvaluationRequest
        {
            Subject = new AuthZen.Models.AuthZenSubject { Id = user },
            Action = new AuthZen.Models.AuthZenAction { Name = relation },
            Resource = new AuthZen.Models.AuthZenResource { Id = obj }
        }, cancellationToken).ContinueWith(t => new FgaCheckResponse
        {
            Allowed = t.Result.Decision,
            Resolution = t.Result.Context?.Reason
        }, cancellationToken);
    }

    public Task<FgaServerStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _adapter.GetServerStatusAsync(cancellationToken);
    }
}
