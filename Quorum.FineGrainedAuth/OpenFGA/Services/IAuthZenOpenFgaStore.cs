using Quorum.FineGrainedAuth.OpenFGA.Models;

namespace Quorum.FineGrainedAuth.OpenFGA.Services;

/// <summary>
/// Interfejs magazynu reguł autoryzacyjnych definiowanych w standardzie AuthZEN
/// i synchronizowanych za pomocą adaptera z serwerem OpenFGA REST API.
/// </summary>
public interface IAuthZenOpenFgaStore
{
    Task<IReadOnlyList<AuthZenRuleDto>> GetRulesAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<AuthZenRuleDto?> GetRuleByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AuthZenRuleSyncResult> CreateRuleAsync(AuthZenRuleDto rule, CancellationToken cancellationToken = default);
    Task<AuthZenRuleSyncResult> UpdateRuleAsync(string id, AuthZenRuleDto rule, CancellationToken cancellationToken = default);
    Task<AuthZenRuleSyncResult> DeleteRuleAsync(string id, CancellationToken cancellationToken = default);
    Task<AuthZenRuleSyncResult> SyncRuleAsync(string id, CancellationToken cancellationToken = default);
    Task<AuthZenRuleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default);
    Task<FgaCheckResponse> CheckAccessAsync(string user, string relation, string obj, CancellationToken cancellationToken = default);
    Task<FgaServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
