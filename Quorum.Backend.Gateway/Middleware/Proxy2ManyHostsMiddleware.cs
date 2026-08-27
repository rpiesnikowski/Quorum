using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
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

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var uri = context.Request.GetDisplayUrl();
        var method = context.Request.Method;

        // 1. Pobranie aktywnych reguł z bazy, posortowanych według priorytetu
        var activeRoutes = await dbContext.GatewayRoutes
            .Include(r => r.Scopes)
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        // 2. Dopasowanie trasy za pomocą Regex i metody HTTP
        GatewayRoute? matchedRoute = null;
        foreach (var route in activeRoutes)
        {
            if (IsHttpMethodAllowed(route.HttpMethods, method) &&
                Regex.IsMatch(uri, route.MatchPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
            {
                matchedRoute = route;
                break;
            }
        }

        // Jeśli żaden regex nie pasuje, przekazujemy żądanie dalej w potoku ASP.NET
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

        // 4. Konstruowanie docelowego URI
        var targetUri = BuildTargetUri(context.Request, matchedRoute).ToString().TrimEnd('/') ?? "";

        // 5. Przygotowanie żądania proxy (HttpRequestMessage)
        using var proxyRequest = new HttpRequestMessage(new HttpMethod(method), targetUri);

        // Kopiowanie nagłówków przychodzących
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.StartsWith(":") || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && proxyRequest.Content != null)
            {
                proxyRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Wstrzykiwanie niestandardowych nagłówków skonfigurowanych w regule trasy (route.Headers)
        if (!string.IsNullOrWhiteSpace(matchedRoute.Headers))
        {
            var headerLines = matchedRoute.Headers.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var hLine in headerLines)
            {
                var separatorIdx = hLine.IndexOf(':');
                if (separatorIdx > 0)
                {
                    var hKey = hLine.Substring(0, separatorIdx).Trim();
                    var hVal = hLine.Substring(separatorIdx + 1).Trim();
                    if (!string.IsNullOrEmpty(hKey))
                    {
                        proxyRequest.Headers.TryAddWithoutValidation(hKey, hVal);
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
            proxyRequest.Headers.Host = matchedRoute.AddressPort is 80 or 443
                ? matchedRoute.AddressHost
                : $"{matchedRoute.AddressHost}:{matchedRoute.AddressPort}";
        }

        // Kopiowanie treści żądania (Body) dla metod POST, PUT, PATCH itp.
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method))
        {
            var streamContent = new StreamContent(context.Request.Body);
            if (context.Request.ContentType != null)
            {
                streamContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
            proxyRequest.Content = streamContent;
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

    private static Uri BuildTargetUri(HttpRequest request, GatewayRoute route)
    {
        var basePath = route.AddressBasePath?.TrimEnd('/') ?? string.Empty;
        var path = !string.IsNullOrWhiteSpace(route.AddressPath) 
            ? route.AddressPath 
            : request.Path.Value;

        var queryString = !string.IsNullOrWhiteSpace(route.AddressQueryString)
            ? route.AddressQueryString
            : request.QueryString.Value;

        var builder = new UriBuilder
        {
            Scheme = route.Scheme,
            Host = route.AddressHost,
            Port = route.AddressPort,
            Path = $"{basePath}{path}",
            Query = queryString?.TrimStart('?')
        };

        return builder.Uri;
    }
}
// Extension Method ułatwiająca rejestrację middleware w Program.cs