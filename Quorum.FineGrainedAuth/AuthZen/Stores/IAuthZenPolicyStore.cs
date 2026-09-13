using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Stores;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public interface IAuthZenPolicyStore
{
    Task<PagedResult<AuthZenPolicyAdminModel>> GetPoliciesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<AuthZenPolicyAdminModel?> GetPolicyByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeletePolicyAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> TogglePolicyStatusAsync(int id, CancellationToken cancellationToken = default);
}
