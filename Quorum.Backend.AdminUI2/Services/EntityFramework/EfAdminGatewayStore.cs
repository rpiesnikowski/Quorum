using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI2.Models;
using Quorum.Backend.AdminUI2.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using System.Text.RegularExpressions;

namespace Quorum.Backend.AdminUI2.Services.EntityFramework;

public class EfAdminGatewayStore : IAdminGatewayStore
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EfAdminGatewayStore> _logger;

    public EfAdminGatewayStore(
        ApplicationDbContext context,
        ILogger<EfAdminGatewayStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<GatewayRouteAdminModel>> GetRoutesAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GatewayRoutes
            .Include(r => r.Scopes)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r =>
                r.MatchPattern.ToLower().Contains(s) ||
                (r.RouteName != null && r.RouteName.ToLower().Contains(s)) ||
                r.AddressHost.ToLower().Contains(s) ||
                r.Scopes.Any(sc => sc.Scope.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchPattern)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(MapToModel).ToList();
        return new PagedResult<GatewayRouteAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<GatewayRouteAdminModel?> GetRouteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GatewayRoutes
            .Include(r => r.Scopes)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<(bool Success, string? Error)> CreateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default)
    {
        var (scheme, host, port, basePath) = ParseUpstreamUrl(model.UpstreamHost);

        var entity = new GatewayRoute
        {
            MatchPattern = model.PathPattern,
            RouteName = model.Name,
            Description = model.Description,
            Scheme = scheme,
            AddressHost = host,
            AddressPort = port,
            AddressBasePath = basePath,
            AddressPath = model.DownstreamPath,
            TimeoutSeconds = model.TimeoutSeconds,
            HttpMethods = string.Join(",", model.AllowedHttpMethods),
            AllowAnonymous = model.RequiredScopes == null || model.RequiredScopes.Count == 0,
            RequiredScope = model.RequiredScopes != null && model.RequiredScopes.Count > 0,
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            CreatedAt = DateTime.UtcNow
        };

        if (model.RequiredScopes != null)
        {
            foreach (var scope in model.RequiredScopes)
            {
                entity.Scopes.Add(new GatewayRouteScope { Scope = scope });
            }
        }

        _context.GatewayRoutes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateRouteAsync(GatewayRouteAdminModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GatewayRoutes
            .Include(r => r.Scopes)
            .FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Trasa Gateway o ID {model.Id} nie została znaleziona.");
        }

        var (scheme, host, port, basePath) = ParseUpstreamUrl(model.UpstreamHost);

        entity.MatchPattern = model.PathPattern;
        entity.RouteName = model.Name;
        entity.Description = model.Description;
        entity.Scheme = scheme;
        entity.AddressHost = host;
        entity.AddressPort = port;
        entity.AddressBasePath = basePath;
        entity.AddressPath = model.DownstreamPath;
        entity.TimeoutSeconds = model.TimeoutSeconds;
        entity.HttpMethods = string.Join(",", model.AllowedHttpMethods);
        entity.AllowAnonymous = model.RequiredScopes == null || model.RequiredScopes.Count == 0;
        entity.RequiredScope = model.RequiredScopes != null && model.RequiredScopes.Count > 0;
        entity.IsEnabled = model.IsEnabled;
        entity.Priority = model.Priority;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Scopes.Clear();
        foreach (var sc in model.RequiredScopes ?? new())
        {
            entity.Scopes.Add(new GatewayRouteScope { Scope = sc });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteRouteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null) return (true, null);

        _context.GatewayRoutes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<GatewayTestResult> TestRouteAsync(GatewayTestRequest request, CancellationToken cancellationToken = default)
    {
        var routes = await _context.GatewayRoutes
            .Include(r => r.Scopes)
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

        foreach (var r in routes)
        {
            bool isMatch = false;
            try
            {
                if (r.MatchPattern.StartsWith("^") || r.MatchPattern.Contains(".*"))
                {
                    isMatch = Regex.IsMatch(request.RequestPath, r.MatchPattern, RegexOptions.IgnoreCase);
                }
                else
                {
                    isMatch = request.RequestPath.StartsWith(r.MatchPattern, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                isMatch = false;
            }

            if (!isMatch) continue;

            // Check HTTP methods
            if (!string.Equals(r.HttpMethods, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var methods = r.HttpMethods.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (!methods.Contains(request.HttpMethod, StringComparer.OrdinalIgnoreCase))
                {
                    continue; // method mismatch
                }
            }

            // Route matched! Check scopes
            var routeModel = MapToModel(r);
            var missing = routeModel.RequiredScopes.Except(request.ProvidedScopes, StringComparer.OrdinalIgnoreCase).ToList();
            bool isAuthorized = missing.Count == 0;

            var targetPortStr = (r.AddressPort == 80 && r.Scheme == "http") || (r.AddressPort == 443 && r.Scheme == "https") ? "" : $":{r.AddressPort}";
            var targetUri = $"{r.Scheme}://{r.AddressHost}{targetPortStr}{r.AddressBasePath}{request.RequestPath}";

            return new GatewayTestResult
            {
                IsMatch = true,
                MatchedRoute = routeModel,
                TargetUri = targetUri,
                IsAuthorized = isAuthorized,
                MissingScopes = missing,
                Explanation = isAuthorized 
                    ? $"Żądanie pomyślnie dopasowane do trasy '{r.RouteName ?? r.MatchPattern}' (Priorytet {r.Priority}). Wszystkie wymagane scopes zostały spełnione."
                    : $"Żądanie dopasowane do trasy '{r.RouteName ?? r.MatchPattern}', lecz brakuje wymaganych uprawnień: {string.Join(", ", missing)}."
            };
        }

        return new GatewayTestResult
        {
            IsMatch = false,
            Explanation = "Żadna aktywna trasa nie pasuje do podanej ścieżki i metody HTTP."
        };
    }

    private static (string scheme, string host, int port, string basePath) ParseUpstreamUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme;
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (scheme == "https" ? 443 : 80);
            var path = uri.AbsolutePath.TrimEnd('/');
            return (scheme, host, port, path);
        }

        return ("http", "localhost", 5001, "");
    }

    private static GatewayRouteAdminModel MapToModel(GatewayRoute r)
    {
        var targetPort = (r.AddressPort == 80 && r.Scheme == "http") || (r.AddressPort == 443 && r.Scheme == "https") ? "" : $":{r.AddressPort}";
        var upstream = $"{r.Scheme}://{r.AddressHost}{targetPort}{r.AddressBasePath}";

        return new GatewayRouteAdminModel
        {
            Id = r.Id,
            Name = r.RouteName ?? r.MatchPattern,
            Description = r.Description,
            PathPattern = r.MatchPattern,
            IsRegex = r.MatchPattern.StartsWith("^"),
            Priority = r.Priority,
            UpstreamHost = upstream,
            DownstreamPath = r.AddressPath,
            TimeoutSeconds = r.TimeoutSeconds,
            IsEnabled = r.IsEnabled,
            AllowedHttpMethods = r.HttpMethods.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
            RequiredScopes = r.Scopes?.Select(s => s.Scope).ToList() ?? new()
        };
    }
}
