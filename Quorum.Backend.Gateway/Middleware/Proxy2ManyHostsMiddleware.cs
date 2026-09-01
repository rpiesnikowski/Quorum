using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend.Gateway.Services;
using Microsoft.AspNetCore.Http.Extensions;

namespace Quorum.Backend.Gateway.Middleware;

public class Proxy2ManyHostsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Proxy2ManyHostsMiddleware> _logger;

    public Proxy2ManyHostsMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        ILogger<Proxy2ManyHostsMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IGatewayRouteCache routeCache)
    {
        var uri = context.Request.GetDisplayUrl();
        var method = context.Request.Method;

        // 1. Pobranie aktywnych reguł z pamięci podręcznej (in-memory cache synchronizowany przez SignalR)
        var activeRoutes = await routeCache.GetActiveRoutesAsync(context.RequestAborted);

        // 2. Dopasowanie trasy za pomocą GatewayRouteMatcher (Regex, szablony {grupa}, prefiksy) i metody HTTP
        GatewayRoute? matchedRoute = null;
        Match? matchedMatch = null;
        Dictionary<string, string> capturedGroups = new(StringComparer.OrdinalIgnoreCase);

        var requestPath = context.Request.Path.Value ?? "/";

        foreach (var route in activeRoutes)
        {
            if (IsHttpMethodAllowed(route.HttpMethods, method) &&
                GatewayRouteMatcher.TryMatch(route.MatchPattern, requestPath, uri, out var match, out var groups))
            {
                matchedRoute = route;
                matchedMatch = match;
                capturedGroups = groups;
                break;
            }
        }

        // Jeśli żaden wzorzec nie pasuje, przekazujemy żądanie dalej w potoku ASP.NET
        if (matchedRoute == null)
        {
            await _next(context);
            return;
        }

        // 3. Weryfikacja Autoryzacji i Scope
        if (!matchedRoute.AllowAnonymous)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Headers.Append("WWW-Authenticate", $"{matchedRoute.AuthenticationSchemes ?? "Bearer"} realm=\"Access to Gateway\"");
                return;
            }

            if (matchedRoute.RequiredScope)
            {
                var requiredScopes = new List<string>();
                if (matchedRoute.Scopes != null && matchedRoute.Scopes.Count > 0)
                {
                    requiredScopes.AddRange(matchedRoute.Scopes.Select(s => s.Scope));
                }
                else if (!string.IsNullOrWhiteSpace(matchedRoute.ScopeName))
                {
                    requiredScopes.AddRange(matchedRoute.ScopeName.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                }

                if (requiredScopes.Count > 0)
                {
                    var userScopes = context.User.FindAll("scope")
                        .Concat(context.User.FindAll("scp"))
                        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var missingScopes = requiredScopes.Where(rs => !userScopes.Contains(rs)).ToList();
                    if (missingScopes.Count > 0)
                    {
                        _logger.LogWarning("Brak wymaganych scopes: {MissingScopes} dla żądania {Path}", string.Join(", ", missingScopes), uri);
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.Headers.Append("WWW-Authenticate", $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(" ", missingScopes)}\"");
                        return;
                    }
                }
            }
        }

        // 4. Konstruowanie docelowego URI z podstawieniem grup Regex / Szablonu
        var targetUri = BuildTargetUri(context.Request, matchedRoute, matchedMatch, capturedGroups).ToString().TrimEnd('/') ?? "";

        // 5. Przygotowanie żądania proxy (HttpRequestMessage)
        using var proxyRequest = new HttpRequestMessage(new HttpMethod(method), targetUri);

        // Kopiowanie i ewentualna transformacja treści żądania (Body) dla metod zawierających ciało
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            if (!string.IsNullOrWhiteSpace(matchedRoute.Body))
            {
                if (GatewayRouteMatcher.IsEmptyValue(matchedRoute.Body))
                {
                    // Jawne (empty) = całkowite usunięcie treści żądania
                    proxyRequest.Content = null;
                }
                else
                {
                    // Odczyt wejściowej treści żądania
                    using var reader = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    var rawBody = await reader.ReadToEndAsync();

                    var headerDict = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                    var transformedBody = GatewayBodyTransformer.Transform(
                        rawBody,
                        matchedRoute.Body,
                        matchedRoute.BodyTransformType,
                        matchedMatch,
                        capturedGroups,
                        headerDict,
                        out var transformError);

                    if (!string.IsNullOrEmpty(transformError))
                    {
                        _logger.LogWarning("Błąd transformacji Body dla trasy {RouteId}: {Error}", matchedRoute.Id, transformError);
                    }

                    var mediaType = !string.IsNullOrWhiteSpace(context.Request.ContentType)
                        ? context.Request.ContentType
                        : "application/json";

                    proxyRequest.Content = new StringContent(transformedBody ?? "", System.Text.Encoding.UTF8, mediaType);
                }
            }
            else
            {
                // Brak zdefiniowanego szablonu = przekazywanie strumienia wejściowego bez zmian
                var streamContent = new StreamContent(context.Request.Body);
                if (context.Request.ContentType != null)
                {
                    streamContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                }
                proxyRequest.Content = streamContent;
            }
        }

        // Kopiowanie nagłówków przychodzących od klienta
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.StartsWith(":") || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && proxyRequest.Content != null)
            {
                proxyRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Wstrzykiwanie lub usuwanie nagłówków skonfigurowanych w regule trasy (route.Headers)
        // Jeśli wartość nagłówka to (empty), nagłówek jest usuwany przed przekazaniem do upstream (np. Authorization: (empty))
        if (!string.IsNullOrWhiteSpace(matchedRoute.Headers))
        {
            var headerLines = matchedRoute.Headers.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var hLine in headerLines)
            {
                var separatorIdx = hLine.IndexOf(':');
                if (separatorIdx > 0)
                {
                    var hKey = GatewayRouteMatcher.ApplyReplacements(hLine.Substring(0, separatorIdx).Trim(), matchedMatch, capturedGroups);
                    var hVal = GatewayRouteMatcher.ApplyReplacements(hLine.Substring(separatorIdx + 1).Trim(), matchedMatch, capturedGroups);
                    if (!string.IsNullOrEmpty(hKey))
                    {
                        if (GatewayRouteMatcher.IsEmptyValue(hVal))
                        {
                            // Jawne (empty) = usunięcie nagłówka z zapytania upstream
                            proxyRequest.Headers.Remove(hKey);
                            if (proxyRequest.Content != null)
                            {
                                proxyRequest.Content.Headers.Remove(hKey);
                            }
                        }
                        else
                        {
                            proxyRequest.Headers.Remove(hKey);
                            if (!proxyRequest.Headers.TryAddWithoutValidation(hKey, hVal) && proxyRequest.Content != null)
                            {
                                proxyRequest.Content.Headers.Remove(hKey);
                                proxyRequest.Content.Headers.TryAddWithoutValidation(hKey, hVal);
                            }
                        }
                    }
                }
            }
        }

        // Modyfikacja / nadpisanie nagłówka Host
        if (matchedRoute.ForwardOriginalHost)
        {
            proxyRequest.Headers.Host = context.Request.Host.Value;
        }
        else
        {
            var resolvedHost = GatewayRouteMatcher.ApplyReplacements(matchedRoute.AddressHost, matchedMatch, capturedGroups);
            if (GatewayRouteMatcher.IsEmptyValue(resolvedHost))
            {
                resolvedHost = "localhost";
            }
            proxyRequest.Headers.Host = matchedRoute.AddressPort is 80 or 443
                ? resolvedHost
                : $"{resolvedHost}:{matchedRoute.AddressPort}";
        }

        // 6. Wykonanie zapytania proxy z uwzględnieniem Timeout
        var client = _httpClientFactory.CreateClient("GatewayProxyClient");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(matchedRoute.TimeoutSeconds > 0 ? matchedRoute.TimeoutSeconds : 30));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, context.RequestAborted);

        try
        {
            using var responseMessage = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, combinedCts.Token);

            // 7. Przepisanie odpowiedzi z usługi docelowej do klienta
            context.Response.StatusCode = (int)responseMessage.StatusCode;

            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Usunięcie nagłówka transfer-encoding w celu uniknięcia konfliktów
            context.Response.Headers.Remove("transfer-encoding");

            await responseMessage.Content.CopyToAsync(context.Response.Body, combinedCts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError("Przekroczono limit czasu proxy ({Timeout}s) dla {TargetUri}", matchedRoute.TimeoutSeconds, targetUri);
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przekierowywania żądania do {TargetUri}", targetUri);
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
    }

    private static bool IsHttpMethodAllowed(string allowedMethods, string currentMethod)
    {
        if (string.IsNullOrWhiteSpace(allowedMethods) || allowedMethods.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            return true;

        var methods = allowedMethods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return methods.Contains(currentMethod, StringComparer.OrdinalIgnoreCase);
    }

    private static Uri BuildTargetUri(
        HttpRequest request,
        GatewayRoute route,
        Match? match,
        IReadOnlyDictionary<string, string> capturedGroups)
    {
        var path = request.Path.Value ?? "/";
        var query = request.QueryString.Value;
        return GatewayRouteMatcher.BuildTargetUri(path, query, route, match, capturedGroups);
    }
}
// Extension Method ułatwiająca rejestrację middleware w Program.cs