using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.Gateway.Services;

/// <summary>
/// Interfejs pamięci podręcznej reguł routingu API Gateway w pamięci RAM.
/// Zapewnia natychmiastowe dopasowywanie tras oraz inwalidację/przeładowanie po odebraniu sygnału SignalR.
/// </summary>
public interface IGatewayRouteCache
{
    /// <summary>
    /// Pobiera aktualną listę aktywnych reguł routingu posortowanych według priorytetu malejąco.
    /// </summary>
    Task<IReadOnlyList<GatewayRoute>> GetActiveRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Wymusza natychmiastowe odświeżenie reguł z bazy danych do pamięci RAM.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Oznacza pamięć podręczną jako unieważnioną.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Data i czas ostatniego pomyślnego załadowania reguł.
    /// </summary>
    DateTime LastRefreshedUtc { get; }

    /// <summary>
    /// Liczba aktualnie załadowanych reguł w pamięci.
    /// </summary>
    int RouteCount { get; }
}

/// <summary>
/// Wątkowo-bezpieczna implementacja InMemory Cache dla tras API Gateway.
/// </summary>
public class GatewayRouteCache : IGatewayRouteCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GatewayRouteCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private volatile IReadOnlyList<GatewayRoute> _cachedRoutes = Array.Empty<GatewayRoute>();
    private volatile bool _isInitialized = false;

    public DateTime LastRefreshedUtc { get; private set; } = DateTime.MinValue;
    public int RouteCount => _cachedRoutes.Count;

    public GatewayRouteCache(
        IServiceScopeFactory scopeFactory,
        ILogger<GatewayRouteCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GatewayRoute>> GetActiveRoutesAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized && _cachedRoutes.Count > 0)
        {
            return _cachedRoutes;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_isInitialized)
            {
                await ReloadFromDbAsync(cancellationToken);
            }
            return _cachedRoutes;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await ReloadFromDbAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _isInitialized = false;
        _logger.LogInformation("[GatewayRouteCache] Pamięć podręczna reguł Gateway została oznaczona jako unieważniona.");
    }

    private async Task ReloadFromDbAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var routes = await dbContext.GatewayRoutes
                .Include(r => r.Scopes)
                .AsNoTracking()
                .Where(r => r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .ToListAsync(cancellationToken);

            _cachedRoutes = routes.AsReadOnly();
            _isInitialized = true;
            LastRefreshedUtc = DateTime.UtcNow;

            _logger.LogInformation("[GatewayRouteCache] Załadowano i zbuforowano {Count} aktywnych reguł routingu API Gateway.", _cachedRoutes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GatewayRouteCache] Wystąpił błąd podczas ładowania reguł routingu z bazy danych.");
            // Zachowujemy poprzednią wersję w pamięci RAM w razie awarii bazy danych (Circuit breaker/Graceful fallback)
        }
    }
}
