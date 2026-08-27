using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Silnik dopasowywania wzorców tras (MatchPattern) z obsługą wyrażeń regularnych (Regex),
/// grup nazwanych (?&lt;grupa&gt;...), szablonów ścieżek ({grupa1}/{grupa2}) oraz dynamicznego
/// podstawiania parametrów w adresach upstream backendu.
/// </summary>
public static class GatewayRouteMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex RouteTemplateParamRegex = new(@"\{(\*{0,2})([a-zA-Z0-9_]+)(?::([a-zA-Z0-9_]+))?\}", RegexOptions.Compiled);
    private static readonly Regex PlaceholderReplacementRegex = new(@"\{(?<name>[a-zA-Z0-9_]+)\}|\$\{(?<name>[a-zA-Z0-9_]+)\}|\$(?<name>\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Sprawdza czy podany ciąg to szablon z parametrami w nawiasach klamrowych np. /api/v1/{grupa1}/{grupa2}
    /// </summary>
    public static bool IsTemplatePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        return RouteTemplateParamRegex.IsMatch(pattern);
    }

    /// <summary>
    /// Konwertuje wzorzec trasy (szablon {param}, wyrażenie regularne lub prefiks) na skompilowany obiekt Regex.
    /// </summary>
    public static Regex CompilePattern(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, p =>
        {
            var trimmed = p.Trim();

            // Przypadek 1: Szablon z parametrami w klamrach np. /api/v1/{grupa1}/{grupa2}
            if (IsTemplatePattern(trimmed))
            {
                var sb = new StringBuilder();
                if (!trimmed.StartsWith("^"))
                {
                    sb.Append("^");
                }

                int lastIndex = 0;
                var matches = RouteTemplateParamRegex.Matches(trimmed);
                foreach (Match m in matches)
                {
                    // Dodaj dosłowną część przed parametrem (z ucieczką znaków specjalnych poza '/')
                    var literalPart = trimmed.Substring(lastIndex, m.Index - lastIndex);
                    sb.Append(EscapeRegexLiteral(literalPart));

                    var wildcard = m.Groups[1].Value; // "", "*", "**"
                    var paramName = m.Groups[2].Value; // "grupa1"
                    var constraint = m.Groups[3].Value.ToLowerInvariant(); // "int", "guid" itp.

                    if (wildcard == "**" || wildcard == "*")
                    {
                        // Catch-all (wszystko łącznie ze slashami)
                        sb.Append($"(?<{paramName}>.*)");
                    }
                    else if (constraint == "int" || constraint == "digits" || constraint == "d")
                    {
                        sb.Append($"(?<{paramName}>\\d+)");
                    }
                    else if (constraint == "guid")
                    {
                        sb.Append($"(?<{paramName}>[0-9a-fA-F-]{{36}})");
                    }
                    else
                    {
                        // Standardowy segment ścieżki (do kolejnego slasha, znaku zapytania lub hasha)
                        sb.Append($"(?<{paramName}>[^/?#]+)");
                    }

                    lastIndex = m.Index + m.Length;
                }

                if (lastIndex < trimmed.Length)
                {
                    var tailLiteral = trimmed.Substring(lastIndex);
                    sb.Append(EscapeRegexLiteral(tailLiteral));
                }

                if (!trimmed.EndsWith("$"))
                {
                    // Pozwala na dopasowanie końca ścieżki lub opcjonalnego podciągu
                    sb.Append("(?:[/?#].*)?$");
                }

                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
            }

            // Przypadek 2: Surowy Regex (np. ^/api/v1/(?<grupa1>[^/]+)/(?<grupa2>[^/]+) lub /api/orders/.*)
            if (trimmed.StartsWith("^") || trimmed.Contains(".*") || trimmed.Contains("(?<") || trimmed.Contains("([^"))
            {
                var regexPattern = trimmed;
                return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
            }

            // Przypadek 3: Prosty prefiks ścieżki (np. /api/v1/orders)
            var escapedPrefix = Regex.Escape(trimmed);
            var prefixPattern = $"^{escapedPrefix}(?:[/?#].*)?$";
            return new Regex(prefixPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
        });
    }

    private static string EscapeRegexLiteral(string literal)
    {
        // Uciekamy znaki specjalne regex poza standardowymi separatorami URL
        var sb = new StringBuilder();
        foreach (var c in literal)
        {
            if (c is '.' or '$' or '^' or '{' or '}' or '[' or ']' or '(' or ')' or '+' or '*' or '?' or '\\' or '|')
            {
                sb.Append('\\').Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Próbuje dopasować ścieżkę żądania do wzorca MatchPattern i wyciąga wszystkie grupy.
    /// </summary>
    public static bool TryMatch(
        string? pattern,
        string requestPath,
        string? requestFullUrl,
        out Match? matchResult,
        out Dictionary<string, string> capturedGroups)
    {
        capturedGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        matchResult = null;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            var regex = CompilePattern(pattern);

            // Najpierw sprawdzamy dopasowanie do znormalizowanej ścieżki URL (np. /api/v1/users/123)
            var match = regex.Match(requestPath);
            if (!match.Success && !string.IsNullOrWhiteSpace(requestFullUrl))
            {
                // Jeśli nie pasuje do samej ścieżki, sprawdzamy pełny URL (przydatne przy wzorcach z domeną)
                match = regex.Match(requestFullUrl);
            }

            if (match.Success)
            {
                matchResult = match;

                // Wyodrębnienie grup nazwanych i indeksowanych
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    var grp = match.Groups[i];
                    if (grp.Success)
                    {
                        var groupName = regex.GroupNameFromNumber(i);
                        if (!string.IsNullOrEmpty(groupName) && groupName != i.ToString())
                        {
                            capturedGroups[groupName] = grp.Value;
                        }
                        capturedGroups[i.ToString()] = grp.Value;
                    }
                }

                return true;
            }
        }
        catch
        {
            // W razie błędu regex (np. niepoprawny wzorzec podany przez użytkownika)
        }

        return false;
    }

    /// <summary>
    /// Podstawia wartości schwytanych grup do szablonu (np. /{grupa1}/{grupa2}/v1/orders lub /$1/$2/orders).
    /// </summary>
    public static string ApplyReplacements(
        string? template,
        Match? match,
        IReadOnlyDictionary<string, string>? capturedGroups)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        if ((capturedGroups == null || capturedGroups.Count == 0) && (match == null || !match.Success))
        {
            return template;
        }

        return PlaceholderReplacementRegex.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;

            if (capturedGroups != null && capturedGroups.TryGetValue(name, out var val))
            {
                return val;
            }

            if (match != null && match.Success)
            {
                var grp = match.Groups[name];
                if (grp != null && grp.Success)
                {
                    return grp.Value;
                }
            }

            // Jeśli nie znaleziono dopasowania, zachowaj oryginalny placeholder
            return m.Value;
        });
    }

    /// <summary>
    /// Buduje pełny obiekt Uri docelowego serwera (Upstream) z uwzględnieniem podstawiania grup Regex.
    /// </summary>
    public static Uri BuildTargetUri(
        string incomingPath,
        string? incomingQueryString,
        GatewayRoute route,
        Match? match,
        IReadOnlyDictionary<string, string>? capturedGroups)
    {
        var scheme = !string.IsNullOrWhiteSpace(route.Scheme) ? route.Scheme : "https";
        var rawHost = !string.IsNullOrWhiteSpace(route.AddressHost) ? route.AddressHost : "localhost";
        var host = ApplyReplacements(rawHost, match, capturedGroups);

        var port = route.AddressPort > 0 ? route.AddressPort : (scheme == "https" ? 443 : 80);

        var rawBasePath = route.AddressBasePath?.TrimEnd('/') ?? string.Empty;
        var basePath = ApplyReplacements(rawBasePath, match, capturedGroups);
        if (!string.IsNullOrEmpty(basePath) && !basePath.StartsWith("/"))
        {
            basePath = "/" + basePath;
        }

        string downstreamPath;
        if (!string.IsNullOrWhiteSpace(route.AddressPath))
        {
            downstreamPath = ApplyReplacements(route.AddressPath, match, capturedGroups);
        }
        else
        {
            downstreamPath = incomingPath;
        }

        if (!downstreamPath.StartsWith("/"))
        {
            downstreamPath = "/" + downstreamPath;
        }

        var mergedQuery = new List<string>();
        if (!string.IsNullOrWhiteSpace(route.AddressQueryString))
        {
            var resolvedRouteQuery = ApplyReplacements(route.AddressQueryString, match, capturedGroups);
            mergedQuery.Add(resolvedRouteQuery.TrimStart('?'));
        }
        if (!string.IsNullOrWhiteSpace(incomingQueryString))
        {
            mergedQuery.Add(incomingQueryString.TrimStart('?'));
        }

        var queryString = mergedQuery.Count > 0 ? string.Join("&", mergedQuery) : null;

        var builder = new UriBuilder
        {
            Scheme = scheme,
            Host = host,
            Port = port,
            Path = $"{basePath}{downstreamPath}",
            Query = queryString
        };

        return builder.Uri;
    }
}
