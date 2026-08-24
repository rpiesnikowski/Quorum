using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services;

public class GatewayPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
}

public interface IGatewayAdminService
{
    Task<GatewayPagedResult<GatewayRoute>> GetRoutesPagedAsync(
        string? searchTerm = null,
        bool? isEnabled = null,
        bool? allowAnonymous = null,
        int pageIndex = 1,
        int pageSize = 10);

    Task<List<GatewayRoute>> GetAllRoutesAsync();
    Task<GatewayRoute?> GetRouteByIdAsync(int id);
    Task<bool> CreateRouteAsync(GatewayRoute route);
    Task<bool> UpdateRouteAsync(GatewayRoute route);
    Task<bool> DeleteRouteAsync(int id);
    Task<bool> ToggleRouteStatusAsync(int id);
    Task<(int Total, int Enabled, int Anonymous, int Protected)> GetStatisticsAsync();
    Task<GatewayEvaluationResult> EvaluateRouteAsync(GatewayTestRequest request);
    Task<GatewayTestResponse> ExecuteGatewayTestAsync(GatewayTestRequest request);
}
