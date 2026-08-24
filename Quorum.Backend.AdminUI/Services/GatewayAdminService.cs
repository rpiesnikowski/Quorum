using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI.Data;
using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services;

public class GatewayAdminService : IGatewayAdminService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GatewayAdminService> _logger;

    public GatewayAdminService(
        ApplicationDbContext dbContext,
        ILogger<GatewayAdminService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<GatewayPagedResult<GatewayRoute>> GetRoutesPagedAsync(
        string? searchTerm = null,
        bool? isEnabled = null,
        bool? allowAnonymous = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<GatewayRoute> query = _dbContext.GatewayRoutes
            .Include(r => r.ApiScope)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(r => 
                r.MatchPattern.ToLower().Contains(term) ||
                (r.RouteName != null && r.RouteName.ToLower().Contains(term)) ||
                r.AddressHost.ToLower().Contains(term) ||
                (r.AddressBasePath != null && r.AddressBasePath.ToLower().Contains(term)) ||
                (r.ScopeName != null && r.ScopeName.ToLower().Contains(term)) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        if (isEnabled.HasValue)
        {
            query = query.Where(r => r.IsEnabled == isEnabled.Value);
        }

        if (allowAnonymous.HasValue)
        {
            query = query.Where(r => r.AllowAnonymous == allowAnonymous.Value);
        }

        // Sortowanie domyślne: Priorytet malejąco, a następnie MatchPattern
        query = query.OrderByDescending(r => r.Priority).ThenBy(r => r.MatchPattern);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new GatewayPagedResult<GatewayRoute>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<List<GatewayRoute>> GetAllRoutesAsync()
    {
        return await _dbContext.GatewayRoutes
            .Include(r => r.ApiScope)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchPattern)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<GatewayRoute?> GetRouteByIdAsync(int id)
    {
        return await _dbContext.GatewayRoutes
            .Include(r => r.ApiScope)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<bool> CreateRouteAsync(GatewayRoute route)
    {
        try
        {
            route.CreatedAt = DateTime.UtcNow;
            route.UpdatedAt = DateTime.UtcNow;
            _dbContext.GatewayRoutes.Add(route);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Utworzono nową regułę Gateway Route [Id: {Id}, Pattern: {Pattern}]", route.Id, route.MatchPattern);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas tworzenia reguły Gateway Route dla wzorca {Pattern}", route.MatchPattern);
            return false;
        }
    }

    public async Task<bool> UpdateRouteAsync(GatewayRoute route)
    {
        try
        {
            var existing = await _dbContext.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == route.Id);
            if (existing == null) return false;

            existing.MatchPattern = route.MatchPattern;
            existing.RouteName = route.RouteName;
            existing.Description = route.Description;
            existing.Scheme = route.Scheme;
            existing.AddressHost = route.AddressHost;
            existing.AddressPort = route.AddressPort;
            existing.AddressBasePath = route.AddressBasePath;
            existing.AddressPath = route.AddressPath;
            existing.AddressQueryString = route.AddressQueryString;
            existing.Headers = route.Headers;
            existing.TimeoutSeconds = route.TimeoutSeconds;
            existing.HttpMethods = route.HttpMethods;
            existing.AllowAnonymous = route.AllowAnonymous;
            existing.RequiredScope = route.RequiredScope;
            existing.ApiScopeId = route.ApiScopeId;
            existing.ScopeName = route.ScopeName;
            existing.AuthenticationSchemes = route.AuthenticationSchemes;
            existing.IsEnabled = route.IsEnabled;
            existing.Priority = route.Priority;
            existing.EnableCaching = route.EnableCaching;
            existing.ForwardOriginalHost = route.ForwardOriginalHost;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Zaktualizowano regułę Gateway Route [Id: {Id}, Pattern: {Pattern}]", route.Id, route.MatchPattern);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas aktualizacji reguły Gateway Route [Id: {Id}]", route.Id);
            return false;
        }
    }

    public async Task<bool> DeleteRouteAsync(int id)
    {
        try
        {
            var route = await _dbContext.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id);
            if (route == null) return false;

            _dbContext.GatewayRoutes.Remove(route);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Usunięto regułę Gateway Route [Id: {Id}, Pattern: {Pattern}]", id, route.MatchPattern);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania reguły Gateway Route [Id: {Id}]", id);
            return false;
        }
    }

    public async Task<bool> ToggleRouteStatusAsync(int id)
    {
        try
        {
            var route = await _dbContext.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id);
            if (route == null) return false;

            route.IsEnabled = !route.IsEnabled;
            route.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Przełączono status reguły Gateway Route [Id: {Id}] na {Status}", id, route.IsEnabled);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przełączania statusu reguły Gateway Route [Id: {Id}]", id);
            return false;
        }
    }

    public async Task<(int Total, int Enabled, int Anonymous, int Protected)> GetStatisticsAsync()
    {
        var total = await _dbContext.GatewayRoutes.CountAsync();
        var enabled = await _dbContext.GatewayRoutes.CountAsync(r => r.IsEnabled);
        var anonymous = await _dbContext.GatewayRoutes.CountAsync(r => r.AllowAnonymous);
        var @protected = await _dbContext.GatewayRoutes.CountAsync(r => !r.AllowAnonymous);

        return (total, enabled, anonymous, @protected);
    }
}
