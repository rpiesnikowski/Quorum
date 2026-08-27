using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using System.Text.RegularExpressions;
using GatewayTestRequest = Quorum.Backend.AdminUI.Models.GatewayTestRequest;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

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
        // Jeśli podano pełny URL w UpstreamHost i nie uzupełniono AddressHost osobno, przelicz
        var scheme = !string.IsNullOrWhiteSpace(model.Scheme) ? model.Scheme : "https";
        var host = !string.IsNullOrWhiteSpace(model.AddressHost) ? model.AddressHost : "localhost";
        var port = model.AddressPort > 0 ? model.AddressPort : (scheme == "https" ? 443 : 80);
        var basePath = model.AddressBasePath;

        var entity = new GatewayRoute
        {
            MatchPattern = model.MatchPattern,
            RouteName = model.RouteName,
            Description = model.Description,
            Scheme = scheme,
            AddressHost = host,
            AddressPort = port,
            AddressBasePath = basePath,
            AddressPath = model.AddressPath,
            AddressQueryString = model.AddressQueryString,
            Headers = model.Headers,
            TimeoutSeconds = model.TimeoutSeconds > 0 ? model.TimeoutSeconds : 30,
            HttpMethods = model.AllowedHttpMethods != null && model.AllowedHttpMethods.Count > 0 ? string.Join(",", model.AllowedHttpMethods) : "ALL",
            AllowAnonymous = model.AllowAnonymous,
            RequiredScope = model.RequiredScope,
            ApiScopeId = model.ApiScopeId,
            ScopeName = model.RequiredScopes != null && model.RequiredScopes.Count > 0 ? string.Join(" ", model.RequiredScopes) : model.ScopeName,
            AuthenticationSchemes = model.AuthenticationSchemes ?? "Bearer",
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            EnableCaching = model.EnableCaching,
            ForwardOriginalHost = model.ForwardOriginalHost,
            CreatedAt = DateTime.UtcNow
        };

        if (model.RequiredScopes != null && model.RequiredScopes.Count > 0)
        {
            foreach (var scope in model.RequiredScopes.Distinct())
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

        var scheme = !string.IsNullOrWhiteSpace(model.Scheme) ? model.Scheme : "https";
        var host = !string.IsNullOrWhiteSpace(model.AddressHost) ? model.AddressHost : "localhost";
        var port = model.AddressPort > 0 ? model.AddressPort : (scheme == "https" ? 443 : 80);

        entity.MatchPattern = model.MatchPattern;
        entity.RouteName = model.RouteName;
        entity.Description = model.Description;
        entity.Scheme = scheme;
        entity.AddressHost = host;
        entity.AddressPort = port;
        entity.AddressBasePath = model.AddressBasePath;
        entity.AddressPath = model.AddressPath;
        entity.AddressQueryString = model.AddressQueryString;
        entity.Headers = model.Headers;
        entity.TimeoutSeconds = model.TimeoutSeconds > 0 ? model.TimeoutSeconds : 30;
        entity.HttpMethods = model.AllowedHttpMethods != null && model.AllowedHttpMethods.Count > 0 ? string.Join(",", model.AllowedHttpMethods) : "ALL";
        entity.AllowAnonymous = model.AllowAnonymous;
        entity.RequiredScope = model.RequiredScope;
        entity.ApiScopeId = model.ApiScopeId;
        entity.ScopeName = model.RequiredScopes != null && model.RequiredScopes.Count > 0 ? string.Join(" ", model.RequiredScopes) : model.ScopeName;
        entity.AuthenticationSchemes = model.AuthenticationSchemes ?? "Bearer";
        entity.IsEnabled = model.IsEnabled;
        entity.Priority = model.Priority;
        entity.EnableCaching = model.EnableCaching;
        entity.ForwardOriginalHost = model.ForwardOriginalHost;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Scopes.Clear();
        if (model.RequiredScopes != null)
        {
            foreach (var sc in model.RequiredScopes.Distinct())
            {
                entity.Scopes.Add(new GatewayRouteScope { Scope = sc });
            }
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

            // Route matched! Check scopes and authorization
            var routeModel = MapToModel(r);
            bool isAuthorized = true;
            var missing = new List<string>();

            if (!r.AllowAnonymous)
            {
                if (routeModel.RequiredScopes.Count > 0)
                {
                    missing = routeModel.RequiredScopes.Except(request.ProvidedScopes, StringComparer.OrdinalIgnoreCase).ToList();
                    isAuthorized = missing.Count == 0;
                }
            }

            var targetPortStr = (r.AddressPort == 80 && r.Scheme == "http") || (r.AddressPort == 443 && r.Scheme == "https") ? "" : $":{r.AddressPort}";
            var basePath = r.AddressBasePath?.TrimEnd('/') ?? "";
            var path = !string.IsNullOrWhiteSpace(r.AddressPath) ? r.AddressPath : request.RequestPath;
            var queryString = !string.IsNullOrWhiteSpace(r.AddressQueryString) ? "?" + r.AddressQueryString.TrimStart('?') : "";
            var targetUri = $"{r.Scheme}://{r.AddressHost}{targetPortStr}{basePath}{path}{queryString}";

            return new GatewayTestResult
            {
                IsMatch = true,
                MatchedRoute = routeModel,
                TargetUri = targetUri,
                IsAuthorized = isAuthorized,
                MissingScopes = missing,
                Explanation = isAuthorized 
                    ? $"Żądanie pomyślnie dopasowane do trasy '{r.RouteName ?? r.MatchPattern}' (Priorytet {r.Priority}). Autoryzacja i reguły routingu są prawidłowe."
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

        return ("https", "localhost", 443, "");
    }

    private static GatewayRouteAdminModel MapToModel(GatewayRoute r)
    {
        var methods = string.IsNullOrWhiteSpace(r.HttpMethods) || r.HttpMethods.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? new List<string> { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" }
            : r.HttpMethods.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        var scopesList = r.Scopes != null && r.Scopes.Count > 0
            ? r.Scopes.Select(s => s.Scope).Distinct().ToList()
            : (!string.IsNullOrWhiteSpace(r.ScopeName)
                ? r.ScopeName.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList()
                : new List<string>());

        return new GatewayRouteAdminModel
        {
            Id = r.Id,
            MatchPattern = r.MatchPattern,
            RouteName = r.RouteName,
            Description = r.Description,
            Scheme = r.Scheme ?? "https",
            AddressHost = r.AddressHost,
            AddressPort = r.AddressPort > 0 ? r.AddressPort : (r.Scheme == "http" ? 80 : 443),
            AddressBasePath = r.AddressBasePath,
            AddressPath = r.AddressPath,
            AddressQueryString = r.AddressQueryString,
            Headers = r.Headers,
            TimeoutSeconds = r.TimeoutSeconds > 0 ? r.TimeoutSeconds : 30,
            AllowAnonymous = r.AllowAnonymous,
            RequiredScope = r.RequiredScope,
            ApiScopeId = r.ApiScopeId,
            ScopeName = r.ScopeName,
            AuthenticationSchemes = r.AuthenticationSchemes ?? "Bearer",
            IsEnabled = r.IsEnabled,
            Priority = r.Priority,
            EnableCaching = r.EnableCaching,
            ForwardOriginalHost = r.ForwardOriginalHost,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            AllowedHttpMethods = methods,
            RequiredScopes = scopesList
        };
    }
}
