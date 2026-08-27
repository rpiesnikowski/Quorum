using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Models;

public class GatewayRouteAdminModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa trasy jest wymagana.")]
    [StringLength(100, ErrorMessage = "Nazwa trasy nie może przekraczać 100 znaków.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Wzorzec ścieżki (PathPattern) jest wymagany.")]
    [StringLength(500, ErrorMessage = "Wzorzec ścieżki nie może przekraczać 500 znaków.")]
    public string PathPattern { get; set; } = "/api/";

    public bool IsRegex { get; set; } = false;

    public int Priority { get; set; } = 0;

    [Required(ErrorMessage = "Adres docelowy (UpstreamHost) jest wymagany.")]
    [Url(ErrorMessage = "Podaj poprawny URL hosta docelowego (np. https://internal-api:5001).")]
    public string UpstreamHost { get; set; } = "http://localhost:5001";

    public string? DownstreamPath { get; set; }

    public bool StripPrefix { get; set; } = false;

    public int TimeoutSeconds { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;

    public bool RequireAllScopes { get; set; } = false;

    public string ScopesSummary => RequiredScopes != null && RequiredScopes.Count > 0 ? string.Join(", ", RequiredScopes) : "Anonimowy";
    public string MethodsSummary => AllowedHttpMethods != null && AllowedHttpMethods.Count > 0 ? string.Join(", ", AllowedHttpMethods) : "Wszystkie";
    public string StatusSummary => IsEnabled ? "Aktywna" : "Wyłączona";

    public List<string> AllowedHttpMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

    public List<string> RequiredScopes { get; set; } = new();

    public string AllowedHttpMethodsText
    {
        get => string.Join(", ", AllowedHttpMethods);
        set => AllowedHttpMethods = (value ?? "")
            .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public string RequiredScopesText
    {
        get => string.Join(", ", RequiredScopes);
        set => RequiredScopes = (value ?? "")
            .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Distinct()
            .ToList();
    }
}

public class GatewayTestRequest
{
    public string RequestPath { get; set; } = "/api/v1/users";
    public string HttpMethod { get; set; } = "GET";
    public List<string> ProvidedScopes { get; set; } = new();

    public string ProvidedScopesText
    {
        get => string.Join(" ", ProvidedScopes);
        set => ProvidedScopes = (value ?? "")
            .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }
}

public class GatewayTestResult
{
    public bool IsMatch { get; set; }
    public GatewayRouteAdminModel? MatchedRoute { get; set; }
    public string TargetUri { get; set; } = string.Empty;
    public bool IsAuthorized { get; set; }
    public List<string> MissingScopes { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
}
