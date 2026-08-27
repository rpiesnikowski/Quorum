using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services.Interfaces;

public interface IAdminUserStore
{
    Task<PagedResult<UserAdminModel>> GetUsersAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<UserAdminModel?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateUserAsync(UserAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateUserAsync(UserAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteUserAsync(string id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(string id, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ToggleLockoutAsync(string id, bool lockAccount, CancellationToken cancellationToken = default);
    Task<List<string>> GetAllRolesAsync(CancellationToken cancellationToken = default);
}

public interface IAdminClientStore
{
    Task<PagedResult<ClientAdminModel>> GetClientsAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<ClientAdminModel?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteClientAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> AddSecretAsync(int clientId, ClientSecretModel secret, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteSecretAsync(int clientId, int secretId, CancellationToken cancellationToken = default);
}

public interface IAdminApiScopeStore
{
    Task<PagedResult<ApiScopeAdminModel>> GetScopesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<ApiScopeAdminModel?> GetScopeByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteScopeAsync(int id, CancellationToken cancellationToken = default);
}

public interface IAdminIdentityResourceStore
{
    Task<PagedResult<IdentityResourceAdminModel>> GetResourcesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<IdentityResourceAdminModel?> GetResourceByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateResourceAsync(IdentityResourceAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateResourceAsync(IdentityResourceAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteResourceAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> SeedStandardResourcesAsync(CancellationToken cancellationToken = default);
}

public interface IAdminFederationStore
{
    Task<PagedResult<FederationAdminModel>> GetProvidersAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<FederationAdminModel?> GetProviderByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateProviderAsync(FederationAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateProviderAsync(FederationAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteProviderAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ToggleStatusAsync(int id, bool isEnabled, CancellationToken cancellationToken = default);
    Task<DiscoveryValidationResult> TestDiscoveryAsync(string authority, CancellationToken cancellationToken = default);
}

public interface IAdminGatewayStore
{
    Task<PagedResult<GatewayRouteAdminModel>> GetRoutesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<GatewayRouteAdminModel?> GetRouteByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteRouteAsync(int id, CancellationToken cancellationToken = default);
    Task<GatewayTestResult> TestRouteAsync(GatewayTestRequest request, CancellationToken cancellationToken = default);
}

public interface IAdminGrantStore
{
    Task<PagedResult<PersistedGrantAdminModel>> GetGrantsAsync(string? search = null, string? type = null, string? clientId = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<PersistedGrantAdminModel?> GetGrantByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RevokeGrantAsync(string key, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RevokeAllForSubjectAsync(string subjectId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RevokeAllForClientAsync(string clientId, CancellationToken cancellationToken = default);
}

public interface IAdminDashboardStore
{
    Task<DashboardStatsModel> GetStatsAsync(CancellationToken cancellationToken = default);
}
