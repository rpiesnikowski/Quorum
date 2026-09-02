using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend.Gateway.Services;
using Quorum.Backend.Gateway.Telemetry;
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
        var requestPath = context.Request.Path.Value ?? "/";

        // Rozpoczęcie śledzenia OpenTelemetry dla żądania bramki
        using var activity = GatewayDiagnostics.ActivitySource.StartActivity(
            "Proxy2ManyHosts:Forward",
            ActivityKind.Server);

        activity?.SetTag(GatewayDiagnostics.Tags.HttpMethod, method);
        activity?.SetTag(GatewayDiagnostics.Tags.UrlFull, uri);
        activity?.SetTag(GatewayDiagnostics.Tags.UrlPath, requestPath);
        activity?.SetTag(GatewayDiagnostics.Tags.UrlScheme, context.Request.Scheme);
        activity?.SetTag(GatewayDiagnostics.Tags.ServerAddress, context.Request.Host.Host);
        if (context.Request.Host.Port.HasValue)
        {
            activity?.SetTag(GatewayDiagnostics.Tags.ServerPort, context.Request.Host.Port.Value);
        }

        GatewayDiagnostics.ActiveRequests.Add(1);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Pobranie aktywnych reguł z pamięci podręcznej (in-memory cache synchronizowany przez SignalR)
            var activeRoutes = await routeCache.GetActiveRoutesAsync(context.RequestAborted);

            // 2. Dopasowanie trasy za pomocą GatewayRouteMatcher (Regex, szablony {grupa}, prefiksy) i metody HTTP
            GatewayRoute? matchedRoute = null;
            Match? matchedMatch = null;
            Dictionary<string, string> capturedGroups = new(StringComparer.OrdinalIgnoreCase);

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
                activity?.SetTag("quorum.gateway.route_matched", false);
                await _next(context);
                return;
            }

            activity?.SetTag("quorum.gateway.route_matched", true);
            activity?.SetTag(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id);
            activity?.SetTag(GatewayDiagnostics.Tags.RoutePattern, matchedRoute.MatchPattern);
            activity?.AddEvent(new ActivityEvent("route.matched", tags: new ActivityTagsCollection
            {
                { "route.id", matchedRoute.Id },
                { "route.pattern", matchedRoute.MatchPattern }
            }));

            // 3. Weryfikacja Autoryzacji i Scope
            if (!matchedRoute.AllowAnonymous)
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    activity?.SetTag(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.Unauthorized);
                    activity?.SetStatus(ActivityStatusCode.Error, "Brak uwierzytelnienia klienta (Unauthorized)");
                    GatewayDiagnostics.RequestsTotal.Add(1,
                        new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                        new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                        new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.Unauthorized));

                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.Headers.Append("WWW-Authenticate", $"{matchedRoute.AuthenticationSchemes ?? "Bearer"} realm=\"Access to Gateway\"");
                    return;
                }

                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    activity?.SetTag(GatewayDiagnostics.Tags.AuthUserId, userId);
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

                        activity?.SetTag(GatewayDiagnostics.Tags.AuthRequiredScope, string.Join(" ", requiredScopes));
                        activity?.SetTag(GatewayDiagnostics.Tags.AuthUserScopes, string.Join(" ", userScopes));

                        var missingScopes = requiredScopes.Where(rs => !userScopes.Contains(rs)).ToList();
                        if (missingScopes.Count > 0)
                        {
                            activity?.SetTag(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.Forbidden);
                            activity?.SetStatus(ActivityStatusCode.Error, $"Brak wymaganych scopes: {string.Join(", ", missingScopes)}");
                            GatewayDiagnostics.RequestsTotal.Add(1,
                                new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                                new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                                new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.Forbidden));

                            _logger.LogWarning("Brak wymaganych scopes: {MissingScopes} dla żądania {Path} | TraceId: {TraceId}", 
                                string.Join(", ", missingScopes), uri, activity?.TraceId.ToString());
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            context.Response.Headers.Append("WWW-Authenticate", $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(" ", missingScopes)}\"");
                            return;
                        }
                    }
                }
            }

            // 4. Konstruowanie docelowego URI z podstawieniem grup Regex / Szablonu
            var targetUri = BuildTargetUri(context.Request, matchedRoute, matchedMatch, capturedGroups).ToString().TrimEnd('/') ?? "";

            activity?.SetTag(GatewayDiagnostics.Tags.UpstreamUrl, targetUri);
            activity?.SetTag(GatewayDiagnostics.Tags.UpstreamHost, matchedRoute.AddressHost);
            activity?.SetTag(GatewayDiagnostics.Tags.UpstreamPort, matchedRoute.AddressPort);
            activity?.SetTag(GatewayDiagnostics.Tags.UpstreamTimeoutSeconds, matchedRoute.TimeoutSeconds);
            activity?.SetTag(GatewayDiagnostics.Tags.ForwardOriginalHost, matchedRoute.ForwardOriginalHost);

            // 5. Przygotowanie żądania proxy (HttpRequestMessage)
            using var proxyRequest = new HttpRequestMessage(new HttpMethod(method), targetUri);

            // Propagacja kontekstu śledzenia W3C TraceContext (Distributed Tracing)
            if (activity != null && activity.Id != null)
            {
                proxyRequest.Headers.Remove("traceparent");
                proxyRequest.Headers.TryAddWithoutValidation("traceparent", activity.Id);

                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    proxyRequest.Headers.Remove("tracestate");
                    proxyRequest.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
                }
            }

            // Kopiowanie i ewentualna transformacja treści żądania (Body) dla metod zawierających ciało
            if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
            {
                if (!string.IsNullOrWhiteSpace(matchedRoute.Body))
                {
                    activity?.SetTag(GatewayDiagnostics.Tags.BodyTransformed, true);
                    activity?.SetTag(GatewayDiagnostics.Tags.BodyTransformType, matchedRoute.BodyTransformType.ToString());
                    GatewayDiagnostics.BodyTransformationsTotal.Add(1,
                        new KeyValuePair<string, object?>("transform_type", matchedRoute.BodyTransformType.ToString()),
                        new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id));

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
                            activity?.AddEvent(new ActivityEvent("body.transform.error", tags: new ActivityTagsCollection
                            {
                                { "error", transformError }
                            }));
                            _logger.LogWarning("Błąd transformacji Body dla trasy {RouteId}: {Error} | TraceId: {TraceId}",
                                matchedRoute.Id, transformError, activity?.TraceId.ToString());
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

            activity?.AddEvent(new ActivityEvent("upstream.request.started", tags: new ActivityTagsCollection
            {
                { "target.uri", targetUri }
            }));

            try
            {
                using var responseMessage = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, combinedCts.Token);

                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                var statusCode = (int)responseMessage.StatusCode;

                activity?.SetTag(GatewayDiagnostics.Tags.HttpResponseStatusCode, statusCode);
                if (statusCode >= 400)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, $"Upstream zwrócił kod błędu {statusCode}");
                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }

                activity?.AddEvent(new ActivityEvent("upstream.response.completed", tags: new ActivityTagsCollection
                {
                    { "status.code", statusCode },
                    { "elapsed.ms", elapsedMs }
                }));

                GatewayDiagnostics.RequestsTotal.Add(1,
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, statusCode));

                GatewayDiagnostics.RequestDuration.Record(elapsedMs,
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, statusCode));

                _logger.LogInformation("Proxy2ManyHosts {Method} {Path} -> {TargetUri} | Status {StatusCode} in {ElapsedMs:F1}ms | TraceId: {TraceId}",
                    method, requestPath, targetUri, statusCode, elapsedMs, activity?.TraceId.ToString());

                // 7. Przepisanie odpowiedzi z usługi docelowej do klienta
                context.Response.StatusCode = statusCode;

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
                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                activity?.SetTag(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.GatewayTimeout);
                activity?.SetTag(GatewayDiagnostics.Tags.ErrorType, "Timeout");
                activity?.SetTag(GatewayDiagnostics.Tags.ErrorMessage, $"Przekroczono limit czasu proxy ({matchedRoute.TimeoutSeconds}s)");
                activity?.SetStatus(ActivityStatusCode.Error, "Upstream Timeout");

                GatewayDiagnostics.ProxyErrorsTotal.Add(1,
                    new KeyValuePair<string, object?>("error_type", "Timeout"),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id));

                GatewayDiagnostics.RequestsTotal.Add(1,
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.GatewayTimeout));

                _logger.LogError("Przekroczono limit czasu proxy ({Timeout}s) dla {TargetUri} | TraceId: {TraceId}",
                    matchedRoute.TimeoutSeconds, targetUri, activity?.TraceId.ToString());
                context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                activity?.SetTag(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.BadGateway);
                activity?.SetTag(GatewayDiagnostics.Tags.ErrorType, ex.GetType().Name);
                activity?.SetTag(GatewayDiagnostics.Tags.ErrorMessage, ex.Message);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                GatewayDiagnostics.ProxyErrorsTotal.Add(1,
                    new KeyValuePair<string, object?>("error_type", ex.GetType().Name),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id));

                GatewayDiagnostics.RequestsTotal.Add(1,
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpMethod, method),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.RouteId, matchedRoute.Id),
                    new KeyValuePair<string, object?>(GatewayDiagnostics.Tags.HttpResponseStatusCode, (int)HttpStatusCode.BadGateway));

                _logger.LogError(ex, "Błąd podczas przekierowywania żądania do {TargetUri} | TraceId: {TraceId}",
                    targetUri, activity?.TraceId.ToString());
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            }
        }
        finally
        {
            GatewayDiagnostics.ActiveRequests.Add(-1);
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