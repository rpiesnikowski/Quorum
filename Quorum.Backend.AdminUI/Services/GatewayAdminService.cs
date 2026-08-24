using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            .Include(r => r.Scopes)
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
                r.Scopes.Any(s => s.Scope.ToLower().Contains(term)) ||
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
            .Include(r => r.Scopes)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchPattern)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<GatewayRoute?> GetRouteByIdAsync(int id)
    {
        return await _dbContext.GatewayRoutes
            .Include(r => r.ApiScope)
            .Include(r => r.Scopes)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<bool> CreateRouteAsync(GatewayRoute route)
    {
        try
        {
            route.CreatedAt = DateTime.UtcNow;
            route.UpdatedAt = DateTime.UtcNow;
            if (route.Scopes.Any() && string.IsNullOrWhiteSpace(route.ScopeName))
            {
                route.ScopeName = string.Join(" ", route.Scopes.Select(s => s.Scope));
            }
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
            var existing = await _dbContext.GatewayRoutes
                .Include(r => r.Scopes)
                .FirstOrDefaultAsync(r => r.Id == route.Id);
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
            existing.AuthenticationSchemes = route.AuthenticationSchemes;
            existing.IsEnabled = route.IsEnabled;
            existing.Priority = route.Priority;
            existing.EnableCaching = route.EnableCaching;
            existing.ForwardOriginalHost = route.ForwardOriginalHost;
            existing.UpdatedAt = DateTime.UtcNow;

            // Synchronizacja kolekcji GatewayRouteScopes
            var incomingScopes = (route.Scopes ?? new List<GatewayRouteScope>())
                .Select(s => s.Scope.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toRemove = existing.Scopes
                .Where(s => !incomingScopes.Contains(s.Scope, StringComparer.OrdinalIgnoreCase))
                .ToList();
            foreach (var rem in toRemove)
            {
                _dbContext.GatewayRouteScopes.Remove(rem);
            }

            var currentExistingScopeNames = existing.Scopes.Select(s => s.Scope).ToList();
            foreach (var newScope in incomingScopes)
            {
                if (!currentExistingScopeNames.Contains(newScope, StringComparer.OrdinalIgnoreCase))
                {
                    existing.Scopes.Add(new GatewayRouteScope
                    {
                        GatewayRouteId = existing.Id,
                        Scope = newScope
                    });
                }
            }

            existing.ScopeName = string.Join(" ", incomingScopes);

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

    public async Task<GatewayEvaluationResult> EvaluateRouteAsync(GatewayTestRequest request)
    {
        var result = new GatewayEvaluationResult();

        // 1. Normalizacja ścieżki i query string
        string rawUrl = (request.RequestUrl ?? string.Empty).Trim();
        string path = "/";
        string? query = null;

        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsedAbsoluteUri))
        {
            path = parsedAbsoluteUri.AbsolutePath;
            query = parsedAbsoluteUri.Query.TrimStart('?');
        }
        else
        {
            var parts = rawUrl.Split('?', 2);
            path = parts[0];
            if (!path.StartsWith("/")) path = "/" + path;
            if (parts.Length > 1) query = parts[1];
        }

        result.NormalizedPath = path;
        result.OriginalQueryString = string.IsNullOrWhiteSpace(query) ? null : query;

        // 2. Parsowanie wejściowych nagłówków
        var inputHeaders = ParseHeaders(request.RawHeaders);

        // 3. Pobranie tras z bazy posortowanych według priorytetu
        var routes = await _dbContext.GatewayRoutes
            .Include(r => r.ApiScope)
            .Include(r => r.Scopes)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchPattern)
            .AsNoTracking()
            .ToListAsync();

        var methodUpper = (request.HttpMethod ?? "GET").Trim().ToUpperInvariant();
        GatewayRoute? winningRoute = null;
        Match? winningMatch = null;

        foreach (var route in routes)
        {
            var eval = new GatewayRouteCandidateEvaluation
            {
                RouteId = route.Id,
                RouteName = route.RouteName,
                MatchPattern = route.MatchPattern,
                Priority = route.Priority,
                IsEnabled = route.IsEnabled,
                AllowedMethods = route.HttpMethods ?? "ALL"
            };

            // Sprawdzenie metody HTTP
            bool methodMatches = false;
            if (string.Equals(route.HttpMethods, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                methodMatches = true;
            }
            else
            {
                var allowed = (route.HttpMethods ?? "")
                    .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim().ToUpperInvariant());
                methodMatches = allowed.Contains(methodUpper);
            }
            eval.IsMethodMatch = methodMatches;

            // Sprawdzenie dopasowania Regex
            bool regexMatches = false;
            Match? matchResult = null;
            try
            {
                matchResult = Regex.Match(path, route.MatchPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                regexMatches = matchResult.Success;
            }
            catch (Exception ex)
            {
                eval.Details = $"Błąd wyrażenia regularnego: {ex.Message}";
            }
            eval.IsRegexMatch = regexMatches;

            // Ocena statusu
            if (!route.IsEnabled)
            {
                eval.EvaluationStatus = "Wyłączona (IsEnabled = false)";
            }
            else if (regexMatches && methodMatches)
            {
                if (winningRoute == null)
                {
                    eval.IsWinner = true;
                    eval.EvaluationStatus = "DOPASOWANO (Zwycięska reguła)";
                    eval.Details = $"Wybrana jako reguła routingu (Priorytet: {route.Priority})";
                    winningRoute = route;
                    winningMatch = matchResult;
                }
                else
                {
                    eval.EvaluationStatus = "Pominięto (Niższy priorytet)";
                    eval.Details = $"Wzorzec i metoda pasują, lecz wyprzedzona przez regułę #{winningRoute.Id} (Priorytet: {winningRoute.Priority})";
                }
            }
            else if (regexMatches && !methodMatches)
            {
                eval.EvaluationStatus = "Niezgodna metoda HTTP";
                eval.Details = $"Wzorzec Regex pasuje, lecz metoda '{methodUpper}' nie znajduje się na liście [{route.HttpMethods}]";
            }
            else
            {
                eval.EvaluationStatus = "Brak dopasowania Regex";
                eval.Details = $"Wzorzec nie pasuje do ścieżki '{path}'";
            }

            result.CandidateEvaluations.Add(eval);
        }

        if (winningRoute == null)
        {
            result.IsMatched = false;
            result.AuthStatusBadge = "badge bg-danger";
            result.AuthSummary = "Brak pasującej aktywnej reguły API Gateway dla podanej ścieżki i metody HTTP.";
            return result;
        }

        result.IsMatched = true;
        result.MatchedRoute = winningRoute;

        // 4. Obliczenie docelowego adresu URL (Target Upstream URI)
        var scheme = string.IsNullOrWhiteSpace(winningRoute.Scheme) ? "https" : winningRoute.Scheme.Trim().ToLowerInvariant();
        var host = (winningRoute.AddressHost ?? "localhost").Trim();
        var port = winningRoute.AddressPort;

        string targetPath;
        if (!string.IsNullOrWhiteSpace(winningRoute.AddressPath))
        {
            targetPath = winningRoute.AddressPath.Trim();
            if (!targetPath.StartsWith("/")) targetPath = "/" + targetPath;
        }
        else if (!string.IsNullOrWhiteSpace(winningRoute.AddressBasePath))
        {
            var basePath = winningRoute.AddressBasePath.Trim().TrimEnd('/');
            if (!basePath.StartsWith("/")) basePath = "/" + basePath;

            // Jeśli Regex zawierał grupę przechwytującą podścieżkę (np. ^/api/v1/users(/.*)?$)
            if (winningMatch != null && winningMatch.Groups.Count > 1 && winningMatch.Groups[1].Success && !string.IsNullOrEmpty(winningMatch.Groups[1].Value))
            {
                var sub = winningMatch.Groups[1].Value.TrimStart('/');
                targetPath = string.IsNullOrEmpty(sub) ? basePath : $"{basePath}/{sub}";
            }
            else
            {
                // Fallback: połączenie bazowej ścieżki z wejściową
                var trimmedPath = path.TrimStart('/');
                targetPath = string.IsNullOrEmpty(trimmedPath) ? basePath : $"{basePath}/{trimmedPath}";
            }
        }
        else
        {
            targetPath = path;
        }

        // Połączenie parametrów Query String
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.OriginalQueryString))
        {
            queryParts.Add(result.OriginalQueryString);
        }
        if (!string.IsNullOrWhiteSpace(winningRoute.AddressQueryString))
        {
            queryParts.Add(winningRoute.AddressQueryString.TrimStart('?'));
        }
        var finalQuery = queryParts.Count > 0 ? string.Join("&", queryParts) : null;

        // Konstrukcja pełnego Upstream URL
        bool isStandardPort = (scheme == "https" && port == 443) || (scheme == "http" && port == 80);
        var portSuffix = isStandardPort ? string.Empty : $":{port}";
        var queryStringSuffix = string.IsNullOrEmpty(finalQuery) ? string.Empty : $"?{finalQuery}";

        result.CalculatedUpstreamUrl = $"{scheme}://{host}{portSuffix}{targetPath}{queryStringSuffix}";

        // 5. Obliczenie docelowych nagłówków HTTP
        foreach (var header in inputHeaders)
        {
            result.CalculatedHeaders[header.Key] = header.Value;
        }

        // Dołączenie skonfigurowanych nagłówków z reguły
        if (!string.IsNullOrWhiteSpace(winningRoute.Headers))
        {
            var routeHeaders = ParseHeaders(winningRoute.Headers);
            foreach (var rh in routeHeaders)
            {
                result.CalculatedHeaders[rh.Key] = rh.Value;
            }
        }

        // Standardowe nagłówki proxy
        result.CalculatedHeaders["X-Forwarded-For"] = "127.0.0.1";
        result.CalculatedHeaders["X-Forwarded-Proto"] = scheme;
        result.CalculatedHeaders["X-Forwarded-Host"] = host;
        result.CalculatedHeaders["X-Gateway-Route-Id"] = winningRoute.Id.ToString();
        result.CalculatedHeaders["X-Gateway-Route-Pattern"] = winningRoute.MatchPattern;

        // 6. Analiza zabezpieczeń i uwierzytelniania
        if (winningRoute.AllowAnonymous)
        {
            result.AuthRequired = false;
            result.AuthPassed = true;
            result.AuthStatusBadge = "badge bg-success";
            result.AuthSummary = "Dostęp publiczny (Anonimowy) - AllowAnonymous = true";
            result.AuthDetails.Add("Reguła nie wymaga tokenu uwierzytelniającego.");
        }
        else
        {
            result.AuthRequired = true;
            var hasAuthHeader = result.CalculatedHeaders.ContainsKey("Authorization") && !string.IsNullOrWhiteSpace(result.CalculatedHeaders["Authorization"]);

            if (hasAuthHeader)
            {
                result.AuthPassed = true;
                result.AuthStatusBadge = "badge bg-primary";
                result.AuthSummary = $"Wymagana autoryzacja - Wykryto nagłówek Authorization (Schematy: {winningRoute.AuthenticationSchemes ?? "Bearer"})";
                result.AuthDetails.Add("Nagłówek 'Authorization' został przekazany do żądania docelowego.");
                if (winningRoute.RequiredScope)
                {
                    var scopesList = winningRoute.Scopes.Any()
                        ? string.Join(", ", winningRoute.Scopes.Select(s => s.Scope))
                        : (winningRoute.ScopeName ?? "ApiScope");
                    result.AuthDetails.Add($"Wymagany scope autoryzacji: [{scopesList}] (weryfikacja claims w mikrousłudze).");
                }
            }
            else
            {
                result.AuthPassed = false;
                result.AuthStatusBadge = "badge bg-warning text-dark";
                result.AuthSummary = $"Brak nagłówka Authorization! Reguła wymaga uwierzytelnienia (Schematy: {winningRoute.AuthenticationSchemes ?? "Bearer"})";
                result.AuthDetails.Add("Żądanie nie zawiera nagłówka Authorization. W rzeczywistym środowisku API Gateway zwróci 401 Unauthorized.");
                if (winningRoute.RequiredScope)
                {
                    var scopesList = winningRoute.Scopes.Any()
                        ? string.Join(", ", winningRoute.Scopes.Select(s => s.Scope))
                        : (winningRoute.ScopeName ?? "ApiScope");
                    result.AuthDetails.Add($"Wymagany scope tokenu: [{scopesList}].");
                }
            }
        }

        return result;
    }

    public async Task<GatewayTestResponse> ExecuteGatewayTestAsync(GatewayTestRequest request)
    {
        var response = new GatewayTestResponse
        {
            Request = request,
            Evaluation = await EvaluateRouteAsync(request)
        };

        if (!response.Evaluation.IsMatched || !request.ExecuteLiveRequest)
        {
            response.Execution.Executed = false;
            return response;
        }

        var matchedRoute = response.Evaluation.MatchedRoute;
        var timeoutSeconds = request.CustomTimeoutSeconds ?? (matchedRoute?.TimeoutSeconds > 0 ? matchedRoute.TimeoutSeconds : 30);

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        if (request.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
        }

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), response.Evaluation.CalculatedUpstreamUrl);

            // Dodanie nagłówków
            var hasBody = !string.IsNullOrEmpty(request.RequestBody) &&
                          !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                          !string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);

            if (hasBody)
            {
                var content = new StringContent(request.RequestBody ?? string.Empty, Encoding.UTF8, request.ContentType ?? "application/json");
                httpRequest.Content = content;
            }

            foreach (var h in response.Evaluation.CalculatedHeaders)
            {
                if (string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Obsługiwane przez HttpContent
                }

                httpRequest.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
            stopwatch.Stop();

            response.Execution.Executed = true;
            response.Execution.StatusCode = (int)httpResponse.StatusCode;
            response.Execution.StatusPhrase = httpResponse.ReasonPhrase ?? httpResponse.StatusCode.ToString();
            response.Execution.IsSuccess = httpResponse.IsSuccessStatusCode;
            response.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // Odczyt nagłówków odpowiedzi
            foreach (var header in httpResponse.Headers)
            {
                response.Execution.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
            }
            if (httpResponse.Content?.Headers != null)
            {
                foreach (var header in httpResponse.Content.Headers)
                {
                    response.Execution.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                response.Execution.ResponseContentType = httpResponse.Content.Headers.ContentType?.ToString();
                response.Execution.ContentLength = httpResponse.Content.Headers.ContentLength;
            }

            // Odczyt ciała odpowiedzi
            if (httpResponse.Content != null)
            {
                var body = await httpResponse.Content.ReadAsStringAsync();
                response.Execution.ResponseBody = body;
                response.Execution.FormattedResponseBody = FormatResponseBody(body, response.Execution.ResponseContentType);
            }
        }
        catch (TaskCanceledException tex)
        {
            stopwatch.Stop();
            response.Execution.Executed = true;
            response.Execution.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            response.Execution.StatusPhrase = "Gateway Timeout";
            response.Execution.IsSuccess = false;
            response.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            response.Execution.ErrorMessage = $"Przekroczono limit czasu połączenia ({timeoutSeconds}s) do serwera docelowego: {response.Evaluation.CalculatedUpstreamUrl}";
            response.Execution.ErrorDetails = tex.ToString();
        }
        catch (HttpRequestException rex)
        {
            stopwatch.Stop();
            response.Execution.Executed = true;
            response.Execution.StatusCode = (int)HttpStatusCode.BadGateway;
            response.Execution.StatusPhrase = "Bad Gateway";
            response.Execution.IsSuccess = false;
            response.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            response.Execution.ErrorMessage = $"Błąd połączenia z serwerem docelowym ({response.Evaluation.CalculatedUpstreamUrl}): {rex.Message}";
            response.Execution.ErrorDetails = rex.ToString();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            response.Execution.Executed = true;
            response.Execution.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.Execution.StatusPhrase = "Internal Error";
            response.Execution.IsSuccess = false;
            response.Execution.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            response.Execution.ErrorMessage = $"Wystąpił nieoczekiwany błąd podczas testowania proxy: {ex.Message}";
            response.Execution.ErrorDetails = ex.ToString();
        }

        return response;
    }

    private static Dictionary<string, string> ParseHeaders(string? rawHeaders)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawHeaders)) return headers;

        var text = rawHeaders.Trim();

        // Próba parsowania jako JSON
        if (text.StartsWith("{") && text.EndsWith("}"))
        {
            try
            {
                var jsonDict = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                if (jsonDict != null)
                {
                    foreach (var kvp in jsonDict)
                    {
                        headers[kvp.Key] = kvp.Value;
                    }
                    return headers;
                }
            }
            catch
            {
                // Kontynuacja jako tekst linia po linii
            }
        }

        // Parsowanie linia po linii (Klucz: Wartość lub Klucz=Wartość)
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                continue;

            int separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex < 0) separatorIndex = trimmed.IndexOf('=');

            if (separatorIndex > 0)
            {
                var key = trimmed.Substring(0, separatorIndex).Trim();
                var value = trimmed.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    headers[key] = value;
                }
            }
        }

        return headers;
    }

    private static string? FormatResponseBody(string? rawBody, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return rawBody;

        if (contentType != null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(rawBody);
                return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return rawBody;
            }
        }

        return rawBody;
    }

    public async Task<List<GatewayRouteScope>> GetAllRouteScopesAsync()
    {
        return await _dbContext.GatewayRouteScopes
            .Include(s => s.GatewayRoute)
            .OrderBy(s => s.GatewayRouteId)
            .ThenBy(s => s.Scope)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> AddScopeToRouteAsync(int routeId, string scopeName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(scopeName)) return false;
            var cleaned = scopeName.Trim();

            var route = await _dbContext.GatewayRoutes
                .Include(r => r.Scopes)
                .FirstOrDefaultAsync(r => r.Id == routeId);

            if (route == null) return false;

            if (!route.Scopes.Any(s => s.Scope.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
            {
                route.Scopes.Add(new GatewayRouteScope
                {
                    GatewayRouteId = route.Id,
                    Scope = cleaned
                });

                route.ScopeName = string.Join(" ", route.Scopes.Select(s => s.Scope));
                route.RequiredScope = true; // Automatycznie włącz RequiredScope gdy przypisywany jest zakres
                route.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Przypisano zakres '{Scope}' do trasy Gateway [Id: {RouteId}]", cleaned, routeId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przypisywania zakresu '{Scope}' do trasy Gateway [Id: {RouteId}]", scopeName, routeId);
            return false;
        }
    }

    public async Task<bool> RemoveScopeFromRouteAsync(int routeId, string scopeName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(scopeName)) return false;
            var cleaned = scopeName.Trim();

            var route = await _dbContext.GatewayRoutes
                .Include(r => r.Scopes)
                .FirstOrDefaultAsync(r => r.Id == routeId);

            if (route == null) return false;

            var toRemove = route.Scopes
                .Where(s => s.Scope.Equals(cleaned, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (toRemove.Any())
            {
                foreach (var item in toRemove)
                {
                    _dbContext.GatewayRouteScopes.Remove(item);
                }

                route.ScopeName = string.Join(" ", route.Scopes
                    .Where(s => !s.Scope.Equals(cleaned, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Scope));

                if (!route.Scopes.Any(s => !s.Scope.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
                {
                    route.RequiredScope = false;
                }

                route.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Usunięto zakres '{Scope}' z trasy Gateway [Id: {RouteId}]", cleaned, routeId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania zakresu '{Scope}' z trasy Gateway [Id: {RouteId}]", scopeName, routeId);
            return false;
        }
    }

    public async Task<bool> RemoveScopeMappingByIdAsync(int mappingId)
    {
        try
        {
            var mapping = await _dbContext.GatewayRouteScopes
                .Include(s => s.GatewayRoute)
                .ThenInclude(r => r!.Scopes)
                .FirstOrDefaultAsync(s => s.Id == mappingId);

            if (mapping == null) return false;

            var route = mapping.GatewayRoute;
            _dbContext.GatewayRouteScopes.Remove(mapping);

            if (route != null)
            {
                var remainingScopes = route.Scopes
                    .Where(s => s.Id != mappingId)
                    .Select(s => s.Scope)
                    .ToList();

                route.ScopeName = remainingScopes.Any() ? string.Join(" ", remainingScopes) : null;
                if (!remainingScopes.Any())
                {
                    route.RequiredScope = false;
                }
                route.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Usunięto mapowanie zakresu [MappingId: {MappingId}]", mappingId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania mapowania zakresu [MappingId: {MappingId}]", mappingId);
            return false;
        }
    }

    public async Task<bool> SetRouteScopesAsync(int routeId, IEnumerable<string> scopes)
    {
        try
        {
            var route = await _dbContext.GatewayRoutes
                .Include(r => r.Scopes)
                .FirstOrDefaultAsync(r => r.Id == routeId);

            if (route == null) return false;

            var incomingScopes = (scopes ?? Enumerable.Empty<string>())
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toRemove = route.Scopes
                .Where(s => !incomingScopes.Contains(s.Scope, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in toRemove)
            {
                _dbContext.GatewayRouteScopes.Remove(item);
            }

            var currentExisting = route.Scopes.Select(s => s.Scope).ToList();
            foreach (var newScope in incomingScopes)
            {
                if (!currentExisting.Contains(newScope, StringComparer.OrdinalIgnoreCase))
                {
                    route.Scopes.Add(new GatewayRouteScope
                    {
                        GatewayRouteId = route.Id,
                        Scope = newScope
                    });
                }
            }

            route.ScopeName = incomingScopes.Any() ? string.Join(" ", incomingScopes) : null;
            route.RequiredScope = incomingScopes.Any();
            route.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Zaktualizowano zestaw zakresów dla trasy Gateway [Id: {RouteId}]: {Scopes}", routeId, string.Join(", ", incomingScopes));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas ustawiania zakresów dla trasy Gateway [Id: {RouteId}]", routeId);
            return false;
        }
    }
}
