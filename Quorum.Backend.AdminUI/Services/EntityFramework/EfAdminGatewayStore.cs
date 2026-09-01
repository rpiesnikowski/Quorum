using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminGatewayStore : IAdminGatewayStore
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EfAdminGatewayStore> _logger;
    private readonly IGatewayNotificationService _notificationService;

    public EfAdminGatewayStore(
        ApplicationDbContext context,
        ILogger<EfAdminGatewayStore> logger,
        IGatewayNotificationService? notificationService = null)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService ?? new NullGatewayNotificationService();
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

        if (model.NotifyGateway)
        {
            try
            {
                await _notificationService.NotifyRoutesChangedAsync(new GatewayRouteNotificationPayload
                {
                    RouteId = entity.Id,
                    Action = "Created",
                    MatchPattern = entity.MatchPattern,
                    TimestampUtc = DateTime.UtcNow,
                    Message = $"Utworzono nową regułę routingu: '{entity.RouteName ?? entity.MatchPattern}'"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się wysłać powiadomienia o utworzeniu trasy {RouteId}", entity.Id);
            }
        }

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

        if (model.NotifyGateway)
        {
            try
            {
                await _notificationService.NotifyRoutesChangedAsync(new GatewayRouteNotificationPayload
                {
                    RouteId = entity.Id,
                    Action = "Updated",
                    MatchPattern = entity.MatchPattern,
                    TimestampUtc = DateTime.UtcNow,
                    Message = $"Zaktualizowano regułę routingu: '{entity.RouteName ?? entity.MatchPattern}'"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się wysłać powiadomienia o aktualizacji trasy {RouteId}", entity.Id);
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteRouteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null) return (true, null);

        var matchPattern = entity.MatchPattern;
        var routeName = entity.RouteName;

        _context.GatewayRoutes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.NotifyRoutesChangedAsync(new GatewayRouteNotificationPayload
            {
                RouteId = id,
                Action = "Deleted",
                MatchPattern = matchPattern,
                TimestampUtc = DateTime.UtcNow,
                Message = $"Usunięto regułę routingu: '{routeName ?? matchPattern}'"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać powiadomienia o usunięciu trasy {RouteId}", id);
        }

        return (true, null);
    }

    public async Task<GatewayTestResult> TestRouteAsync(GatewayTestRequest request, CancellationToken cancellationToken = default)
    {
        var response = new GatewayTestResult
        {
            Request = request
        };

        // 1. Normalizacja ścieżki wejściowej oraz parametrów zapytania
        var rawUrl = (request.RequestUrl ?? request.RequestPath ?? "/").Trim();
        string normalizedPath = rawUrl;
        string? queryString = null;
        string hostHeaderValue = "localhost";

        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsedAbsoluteUri))
        {
            normalizedPath = parsedAbsoluteUri.AbsolutePath;
            queryString = parsedAbsoluteUri.Query?.TrimStart('?');
            hostHeaderValue = parsedAbsoluteUri.Authority;
        }
        else
        {
            var qIndex = rawUrl.IndexOf('?');
            if (qIndex >= 0)
            {
                normalizedPath = rawUrl.Substring(0, qIndex);
                queryString = rawUrl.Substring(qIndex + 1);
            }
        }

        if (!normalizedPath.StartsWith("/"))
        {
            normalizedPath = "/" + normalizedPath;
        }

        response.Evaluation.NormalizedPath = normalizedPath;
        response.Evaluation.OriginalQueryString = queryString;

        // 2. Pobranie wszystkich aktywnych tras z bazy danych posortowanych według priorytetu malejąco
        var activeRoutes = await _context.GatewayRoutes
            .Include(r => r.Scopes)
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchPattern)
            .ToListAsync(cancellationToken);

        GatewayRoute? matchedRoute = null;
        Match? matchedMatch = null;
        Dictionary<string, string> matchedGroups = new(StringComparer.OrdinalIgnoreCase);
        var requestedMethod = (request.HttpMethod ?? "GET").Trim().ToUpperInvariant();

        foreach (var route in activeRoutes)
        {
            var isTemplate = GatewayRouteMatcher.IsTemplatePattern(route.MatchPattern);
            var isRegex = !string.IsNullOrEmpty(route.MatchPattern) && (route.MatchPattern.StartsWith("^") || route.MatchPattern.Contains(".*") || route.MatchPattern.Contains("(?<"));
            bool isPatternMatch = false;
            Match? candidateMatch = null;
            Dictionary<string, string> candidateGroups = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                isPatternMatch = GatewayRouteMatcher.TryMatch(route.MatchPattern, normalizedPath, rawUrl, out candidateMatch, out candidateGroups);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd ewaluacji wzorca {Pattern}", route.MatchPattern);
                isPatternMatch = false;
            }

            var allowedMethods = string.IsNullOrWhiteSpace(route.HttpMethods) || route.HttpMethods.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ALL", "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" }
                : route.HttpMethods.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool isMethodMatch = allowedMethods.Contains("ALL") || allowedMethods.Contains(requestedMethod);
            bool isWinner = false;
            string evalStatus = string.Empty;
            string? details = null;

            if (isPatternMatch && isMethodMatch)
            {
                if (matchedRoute == null)
                {
                    matchedRoute = route;
                    matchedMatch = candidateMatch;
                    matchedGroups = candidateGroups;
                    isWinner = true;
                    evalStatus = "Dopasowano (Zwycięzca)";
                    details = candidateGroups.Count > 0 
                        ? $"Trasa pasuje (Priorytet {route.Priority}). Wykryto grupy: {string.Join(", ", candidateGroups.Select(kv => $"{{{kv.Key}}}='{kv.Value}'"))}."
                        : $"Trasa ma najwyższy priorytet ({route.Priority}) i pasuje do ścieżki '{normalizedPath}'.";
                }
                else
                {
                    evalStatus = "Pominięto (Niższy priorytet)";
                    details = $"Trasa również pasuje, ale reguła '{matchedRoute.RouteName ?? matchedRoute.MatchPattern}' miała wyższy priorytet ({matchedRoute.Priority} > {route.Priority}).";
                }
            }
            else if (!isPatternMatch)
            {
                evalStatus = "Brak dopasowania wzorca";
                details = $"Ścieżka '{normalizedPath}' nie spełnia wzorca '{route.MatchPattern}'.";
            }
            else
            {
                evalStatus = "Niezgodna metoda HTTP";
                details = $"Metoda {requestedMethod} nie jest dozwolona. Dozwolone: {string.Join(", ", allowedMethods)}.";
            }

            response.Evaluation.CandidateEvaluations.Add(new GatewayRouteCandidateEvaluation
            {
                RouteId = route.Id,
                RouteName = route.RouteName,
                MatchPattern = route.MatchPattern,
                Priority = route.Priority,
                IsEnabled = route.IsEnabled,
                AllowedMethods = route.HttpMethods ?? "ALL",
                IsRegexMatch = isRegex || isTemplate,
                IsMethodMatch = isMethodMatch,
                IsWinner = isWinner,
                EvaluationStatus = evalStatus,
                Details = details,
                CapturedGroups = candidateGroups
            });
        }

        // 3. Sprawdzenie czy znaleziono pasującą trasę
        if (matchedRoute == null)
        {
            response.Evaluation.IsMatched = false;
            response.Evaluation.Explanation = $"Żadna aktywna trasa API Gateway nie pasuje do metody {requestedMethod} i ścieżki '{normalizedPath}'.";
            response.Execution.Executed = false;
            response.Execution.ErrorMessage = "Brak pasującej reguły routingu.";
            return response;
        }

        response.Evaluation.IsMatched = true;
        response.Evaluation.MatchedRoute = matchedRoute;
        response.Evaluation.ForwardOriginalHost = matchedRoute.ForwardOriginalHost;
        response.Evaluation.CapturedGroups = matchedGroups;

        // 4. Konstruowanie docelowego Upstream URI z dynamicznym podstawieniem grup
        var targetUriObj = GatewayRouteMatcher.BuildTargetUri(normalizedPath, queryString, matchedRoute, matchedMatch, matchedGroups);
        var targetUri = targetUriObj.ToString().TrimEnd('/');
        response.Evaluation.CalculatedUpstreamUrl = targetUri;

        // 5. Weryfikacja Scopes & Autoryzacji
        var requiredScopes = new List<string>();
        if (matchedRoute.Scopes != null && matchedRoute.Scopes.Count > 0)
        {
            requiredScopes.AddRange(matchedRoute.Scopes.Select(s => s.Scope));
        }
        else if (!string.IsNullOrWhiteSpace(matchedRoute.ScopeName))
        {
            requiredScopes.AddRange(matchedRoute.ScopeName.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        if (matchedRoute.AllowAnonymous)
        {
            response.Evaluation.AuthRequired = false;
            response.Evaluation.AuthPassed = true;
            response.Evaluation.AuthStatusBadge = "badge bg-success";
            response.Evaluation.AuthSummary = "Dostęp Anonimowy (Publiczny) – token JWT nie jest wymagany.";
        }
        else
        {
            response.Evaluation.AuthRequired = true;
            var providedScopesSet = request.ProvidedScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = requiredScopes.Where(s => !providedScopesSet.Contains(s)).ToList();
            response.Evaluation.MissingScopes = missing;

            if (missing.Count == 0)
            {
                response.Evaluation.AuthPassed = true;
                response.Evaluation.AuthStatusBadge = "badge bg-success";
                response.Evaluation.AuthSummary = requiredScopes.Count > 0
                    ? $"Autoryzacja pomyślna – wszystkie wymagane scopes ({string.Join(", ", requiredScopes)}) zostały spełnione."
                    : "Wymagany poprawny token JWT (trasa nie definiuje dodatkowych scopes).";
            }
            else
            {
                response.Evaluation.AuthPassed = false;
                response.Evaluation.AuthStatusBadge = "badge bg-danger";
                response.Evaluation.AuthSummary = $"Odmowa dostępu (403 Forbidden) – brakujące uprawnienia Scopes: {string.Join(", ", missing)}.";
            }
        }

        var groupSummary = matchedGroups.Count > 0 
            ? $" [Grupy: {string.Join(", ", matchedGroups.Select(kv => $"{{{kv.Key}}}='{kv.Value}'"))}]" 
            : "";

        response.Evaluation.Explanation = response.Evaluation.AuthPassed
            ? $"Dopasowano trasę '{matchedRoute.RouteName ?? matchedRoute.MatchPattern}' (Priorytet {matchedRoute.Priority}){groupSummary}. Upstream: {targetUri}"
            : $"Dopasowano trasę '{matchedRoute.RouteName ?? matchedRoute.MatchPattern}', lecz brakuje wymaganych uprawnień: {string.Join(", ", response.Evaluation.MissingScopes)}";

        // 6. Parsowanie niestandardowych nagłówków trasy (route.Headers) z podstawianiem grup
        if (!string.IsNullOrWhiteSpace(matchedRoute.Headers))
        {
            var headerLines = matchedRoute.Headers.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var hLine in headerLines)
            {
                var sep = hLine.IndexOf(':');
                if (sep > 0)
                {
                    var k = GatewayRouteMatcher.ApplyReplacements(hLine.Substring(0, sep).Trim(), matchedMatch, matchedGroups);
                    var v = GatewayRouteMatcher.ApplyReplacements(hLine.Substring(sep + 1).Trim(), matchedMatch, matchedGroups);
                    if (!string.IsNullOrEmpty(k))
                    {
                        if (GatewayRouteMatcher.IsEmptyValue(v))
                        {
                            response.Evaluation.InjectedRouteHeaders[k] = "(usunięty / empty)";
                        }
                        else
                        {
                            response.Evaluation.InjectedRouteHeaders[k] = v;
                        }
                    }
                }
            }
        }

        // 7. Jeśli użytkownik nie wybrał opcji wykonania żądania sieciowego (tylko dry-run sprawdzenie trasy), zwracamy wynik ewaluacji
        if (!request.ExecuteLiveRequest)
        {
            response.Execution.Executed = false;
            return response;
        }

        // 8. RZECZYWISTE WYKONANIE ŻĄDANIA PROXY DO BACKEND SERVICE
        await ExecuteLiveProxyRequestAsync(request, matchedRoute, targetUri, hostHeaderValue, response, cancellationToken);

        return response;
    }

    private async Task ExecuteLiveProxyRequestAsync(
        GatewayTestRequest request,
        GatewayRoute matchedRoute,
        string targetUri,
        string clientHostHeader,
        GatewayTestResult result,
        CancellationToken cancellationToken)
    {
        result.Execution.Executed = true;
        var stopwatch = Stopwatch.StartNew();

        var timeoutSeconds = request.CustomTimeoutSeconds ?? (matchedRoute.TimeoutSeconds > 0 ? matchedRoute.TimeoutSeconds : 30);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        // Konfiguracja HttpClientHandler z opcją ignorowania błędów certyfikatu SSL (dla środowisk deweloperskich i testowych)
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        if (request.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds + 5)
        };

        var method = new HttpMethod(request.HttpMethod.ToUpperInvariant());
        using var proxyRequest = new HttpRequestMessage(method, targetUri);

        // A. Dodawanie nagłówków wejściowych klienta (RawHeaders)
        var rawReqHeaderDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.RawHeaders))
        {
            var lines = request.RawHeaders.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var sep = line.IndexOf(':');
                if (sep > 0)
                {
                    var k = line.Substring(0, sep).Trim();
                    var v = line.Substring(sep + 1).Trim();
                    if (!string.IsNullOrEmpty(k))
                    {
                        rawReqHeaderDict[k] = v;
                    }
                }
            }
        }

        // Jeśli podano BearerToken a nie ma go w RawHeaders, dodaj nagłówek Authorization
        if (!string.IsNullOrWhiteSpace(request.BearerToken) && !rawReqHeaderDict.ContainsKey("Authorization"))
        {
            rawReqHeaderDict["Authorization"] = $"Bearer {request.BearerToken.Trim()}";
        }

        // B. Dodawanie lub usuwanie nagłówków z konfiguracji trasy (route.Headers)
        foreach (var kvp in result.Evaluation.InjectedRouteHeaders)
        {
            if (GatewayRouteMatcher.IsEmptyValue(kvp.Value) || kvp.Value.StartsWith("(usunięty"))
            {
                rawReqHeaderDict.Remove(kvp.Key);
            }
            else
            {
                rawReqHeaderDict[kvp.Key] = kvp.Value;
            }
        }

        // C. Ustawienie nagłówka Host (zgodnie z ForwardOriginalHost)
        if (matchedRoute.ForwardOriginalHost)
        {
            proxyRequest.Headers.Host = clientHostHeader;
            rawReqHeaderDict["Host"] = clientHostHeader;
        }
        else
        {
            if (Uri.TryCreate(targetUri, UriKind.Absolute, out var targetParsedUri))
            {
                proxyRequest.Headers.Host = targetParsedUri.Authority;
                rawReqHeaderDict["Host"] = targetParsedUri.Authority;
            }
        }

        // D. Ustawienie ciała żądania (RequestBody)
        bool hasBody = method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && !string.IsNullOrEmpty(request.RequestBody);
        if (hasBody)
        {
            var mediaType = !string.IsNullOrWhiteSpace(request.ContentType) ? request.ContentType : "application/json";
            proxyRequest.Content = new StringContent(request.RequestBody ?? "", Encoding.UTF8, mediaType);
            rawReqHeaderDict["Content-Type"] = mediaType;
            rawReqHeaderDict["Content-Length"] = Encoding.UTF8.GetByteCount(request.RequestBody ?? "").ToString();
        }

        // Aplikowanie nagłówków do obiektu HttpRequestMessage
        foreach (var kvp in rawReqHeaderDict)
        {
            if (kvp.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            proxyRequest.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        // E. Konstruowanie surowego zrzutu wysłanego żądania (RAW HTTP REQUEST)
        var rawReqBuilder = new StringBuilder();
        var uriObj = new Uri(targetUri);
        var targetPathAndQuery = uriObj.PathAndQuery;
        rawReqBuilder.AppendLine($"{request.HttpMethod.ToUpperInvariant()} {targetPathAndQuery} HTTP/1.1");
        
        foreach (var kvp in rawReqHeaderDict)
        {
            rawReqBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
        }

        if (hasBody)
        {
            rawReqBuilder.AppendLine();
            rawReqBuilder.AppendLine(request.RequestBody);
        }

        result.Execution.RawRequest = rawReqBuilder.ToString().TrimEnd();

        // F. Wysłanie żądania i odbiór odpowiedzi
        try
        {
            var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseContentRead, cts.Token);
            stopwatch.Stop();

            result.Execution.StatusCode = (int)response.StatusCode;
            result.Execution.StatusPhrase = response.ReasonPhrase ?? response.StatusCode.ToString();
            result.Execution.IsSuccess = response.IsSuccessStatusCode;
            result.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // Zbieranie nagłówków odpowiedzi
            foreach (var h in response.Headers)
            {
                result.Execution.ResponseHeaders[h.Key] = string.Join(", ", h.Value);
            }
            if (response.Content != null)
            {
                foreach (var h in response.Content.Headers)
                {
                    result.Execution.ResponseHeaders[h.Key] = string.Join(", ", h.Value);
                }
                result.Execution.ResponseContentType = response.Content.Headers.ContentType?.ToString();
                result.Execution.ContentLength = response.Content.Headers.ContentLength;

                var bodyBytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
                var bodyStr = Encoding.UTF8.GetString(bodyBytes);
                result.Execution.ResponseBody = bodyStr;

                // Formatowanie JSON do czytelnej postaci (Pretty Print)
                if (!string.IsNullOrWhiteSpace(bodyStr) && 
                    (result.Execution.ResponseContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true || 
                     bodyStr.TrimStart().StartsWith("{") || bodyStr.TrimStart().StartsWith("[")))
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(bodyStr);
                        result.Execution.FormattedResponseBody = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        result.Execution.FormattedResponseBody = bodyStr;
                    }
                }
                else
                {
                    result.Execution.FormattedResponseBody = bodyStr;
                }
            }

            // G. Konstruowanie surowego zrzutu odebranej odpowiedzi (RAW HTTP RESPONSE)
            var rawRespBuilder = new StringBuilder();
            rawRespBuilder.AppendLine($"HTTP/1.1 {result.Execution.StatusCode} {result.Execution.StatusPhrase}");
            foreach (var kvp in result.Execution.ResponseHeaders)
            {
                rawRespBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            if (!string.IsNullOrEmpty(result.Execution.ResponseBody))
            {
                rawRespBuilder.AppendLine();
                rawRespBuilder.AppendLine(result.Execution.ResponseBody);
            }

            result.Execution.RawResponse = rawRespBuilder.ToString().TrimEnd();
        }
        catch (TaskCanceledException tex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            result.Execution.IsSuccess = false;
            result.Execution.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            result.Execution.StatusPhrase = "Gateway Timeout";
            result.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Execution.ErrorMessage = $"Przekroczono limit czasu oczekiwania na odpowiedź ({timeoutSeconds}s) od hosta {matchedRoute.AddressHost}:{matchedRoute.AddressPort}.";
            result.Execution.ErrorDetails = tex.ToString();

            var rawErrBuilder = new StringBuilder();
            rawErrBuilder.AppendLine($"HTTP/1.1 504 Gateway Timeout");
            rawErrBuilder.AppendLine($"Date: {DateTime.UtcNow:R}");
            rawErrBuilder.AppendLine($"Content-Type: text/plain; charset=utf-8");
            rawErrBuilder.AppendLine($"X-Gateway-Error: RequestTimeout");
            rawErrBuilder.AppendLine();
            rawErrBuilder.AppendLine($"Błąd 504 Gateway Timeout: Serwer upstream ({targetUri}) nie odpowiedział w zadanym limicie {timeoutSeconds} sekund.");
            result.Execution.RawResponse = rawErrBuilder.ToString();
        }
        catch (HttpRequestException hex)
        {
            stopwatch.Stop();
            result.Execution.IsSuccess = false;
            result.Execution.StatusCode = (int)HttpStatusCode.BadGateway;
            result.Execution.StatusPhrase = "Bad Gateway";
            result.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Execution.ErrorMessage = $"Błąd połączenia z serwerem Upstream ({matchedRoute.AddressHost}:{matchedRoute.AddressPort}): {hex.Message}";
            result.Execution.ErrorDetails = hex.ToString();

            var rawErrBuilder = new StringBuilder();
            rawErrBuilder.AppendLine($"HTTP/1.1 502 Bad Gateway");
            rawErrBuilder.AppendLine($"Date: {DateTime.UtcNow:R}");
            rawErrBuilder.AppendLine($"Content-Type: text/plain; charset=utf-8");
            rawErrBuilder.AppendLine($"X-Gateway-Error: UpstreamConnectionFailed");
            rawErrBuilder.AppendLine();
            rawErrBuilder.AppendLine($"Błąd 502 Bad Gateway: Nie udało się nawiązać połączenia z serwerem docelowym {targetUri}.\nSzczegóły błędu sieciowego: {hex.Message}");
            result.Execution.RawResponse = rawErrBuilder.ToString();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Execution.IsSuccess = false;
            result.Execution.StatusCode = 500;
            result.Execution.StatusPhrase = "Internal Server Error";
            result.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Execution.ErrorMessage = $"Wystąpił nieoczekiwany błąd podczas wykonywania żądania: {ex.Message}";
            result.Execution.ErrorDetails = ex.ToString();

            var rawErrBuilder = new StringBuilder();
            rawErrBuilder.AppendLine($"HTTP/1.1 500 Internal Server Error");
            rawErrBuilder.AppendLine($"Date: {DateTime.UtcNow:R}");
            rawErrBuilder.AppendLine($"Content-Type: text/plain; charset=utf-8");
            rawErrBuilder.AppendLine();
            rawErrBuilder.AppendLine($"Wystąpił błąd wykonania: {ex.Message}");
            result.Execution.RawResponse = rawErrBuilder.ToString();
        }
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

