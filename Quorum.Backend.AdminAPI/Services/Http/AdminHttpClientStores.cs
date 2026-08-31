using System.Net.Http.Json;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminAPI.Services.Http;

public class AdminHttpDashboardStore : IAdminDashboardStore
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AdminHttpDashboardStore(HttpClient http, string baseUrl = "api/admin")
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<DashboardStatsModel> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<DashboardStatsModel>($"{_baseUrl}/dashboard/stats", cancellationToken);
        return response ?? new DashboardStatsModel();
    }
}

public class AdminHttpClientStore : IAdminClientStore
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AdminHttpClientStore(HttpClient http, string baseUrl = "api/admin")
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<PagedResult<ClientAdminModel>> GetClientsAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = $"{_baseUrl}/clients?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _http.GetFromJsonAsync<PagedResult<ClientAdminModel>>(query, cancellationToken);
        return response ?? new PagedResult<ClientAdminModel>();
    }

    public async Task<ClientAdminModel?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<ClientAdminModel>($"{_baseUrl}/clients/{id}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> CreateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/clients", model, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<ClientAdminModel>(cancellationToken: cancellationToken);
            if (created != null) model.Id = created.Id;
            return (true, null);
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> UpdateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"{_baseUrl}/clients/{model.Id}", model, cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> DeleteClientAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/clients/{id}", cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> AddSecretAsync(int clientId, ClientSecretModel secret, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/clients/{clientId}/secrets", secret, cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> DeleteSecretAsync(int clientId, int secretId, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/clients/{clientId}/secrets/{secretId}", cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }
}

public class AdminHttpApiScopeStore : IAdminApiScopeStore
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AdminHttpApiScopeStore(HttpClient http, string baseUrl = "api/admin")
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<PagedResult<ApiScopeAdminModel>> GetScopesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = $"{_baseUrl}/scopes?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _http.GetFromJsonAsync<PagedResult<ApiScopeAdminModel>>(query, cancellationToken);
        return response ?? new PagedResult<ApiScopeAdminModel>();
    }

    public async Task<ApiScopeAdminModel?> GetScopeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiScopeAdminModel>($"{_baseUrl}/scopes/{id}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> CreateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/scopes", model, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<ApiScopeAdminModel>(cancellationToken: cancellationToken);
            if (created != null) model.Id = created.Id;
            return (true, null);
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> UpdateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"{_baseUrl}/scopes/{model.Id}", model, cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> DeleteScopeAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/scopes/{id}", cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }
}

public class AdminHttpGatewayStore : IAdminGatewayStore
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AdminHttpGatewayStore(HttpClient http, string baseUrl = "api/admin")
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<PagedResult<GatewayRouteAdminModel>> GetRoutesAsync(string? search = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = $"{_baseUrl}/gateway/routes?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _http.GetFromJsonAsync<PagedResult<GatewayRouteAdminModel>>(query, cancellationToken);
        return response ?? new PagedResult<GatewayRouteAdminModel>();
    }

    public async Task<GatewayRouteAdminModel?> GetRouteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<GatewayRouteAdminModel>($"{_baseUrl}/gateway/routes/{id}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> CreateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/gateway/routes", model, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<GatewayRouteAdminModel>(cancellationToken: cancellationToken);
            if (created != null) model.Id = created.Id;
            return (true, null);
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> UpdateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"{_baseUrl}/gateway/routes/{model.Id}", model, cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<(bool Success, string? Error)> DeleteRouteAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/gateway/routes/{id}", cancellationToken);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrEmpty(error) ? response.ReasonPhrase : error);
    }

    public async Task<GatewayTestResult> TestRouteAsync(GatewayTestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/gateway/test", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadFromJsonAsync<GatewayTestResult>(cancellationToken: cancellationToken);
            return res ?? new GatewayTestResult();
        }

        return new GatewayTestResult { MatchFound = false };
    }
}
