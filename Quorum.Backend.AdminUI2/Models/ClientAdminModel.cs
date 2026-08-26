using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI2.Models;

public class ClientAdminModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Identyfikator klienta (ClientId) jest wymagany.")]
    [StringLength(200, ErrorMessage = "ClientId nie może przekraczać 200 znaków.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa klienta (ClientName) jest wymagana.")]
    [StringLength(200, ErrorMessage = "ClientName nie może przekraczać 200 znaków.")]
    public string ClientName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ClientUri { get; set; }

    public string? LogoUri { get; set; }

    public bool Enabled { get; set; } = true;

    public bool RequireClientSecret { get; set; } = true;

    public bool RequirePkce { get; set; } = true;

    public bool AllowPlainTextPkce { get; set; } = false;

    public bool RequireConsent { get; set; } = false;

    public bool AllowRememberConsent { get; set; } = true;

    public bool AlwaysIncludeUserClaimsInIdToken { get; set; } = false;

    public bool AllowOfflineAccess { get; set; } = true;

    public int AccessTokenLifetime { get; set; } = 3600; // 1 godzina

    public int IdentityTokenLifetime { get; set; } = 300; // 5 minut

    public int AuthorizationCodeLifetime { get; set; } = 300;

    public int SlidingRefreshTokenLifetime { get; set; } = 1296000; // 15 dni

    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000; // 30 dni

    public string ProtocolType { get; set; } = "oidc";

    // Grant Types
    public List<string> AllowedGrantTypes { get; set; } = new();

    // Scopes
    public List<string> AllowedScopes { get; set; } = new();

    // Redirect URIs
    public List<string> RedirectUris { get; set; } = new();

    // Post Logout Redirect URIs
    public List<string> PostLogoutRedirectUris { get; set; } = new();

    // Allowed CORS Origins
    public List<string> AllowedCorsOrigins { get; set; } = new();

    // Secrets
    public List<ClientSecretModel> ClientSecrets { get; set; } = new();

    // Claims
    public List<ClientClaimModel> Claims { get; set; } = new();

    // Pomocnicze właściwości tekstowe dla formularza (newline/comma separated)
    public string RedirectUrisText
    {
        get => string.Join(Environment.NewLine, RedirectUris);
        set => RedirectUris = ParseList(value);
    }

    public string PostLogoutRedirectUrisText
    {
        get => string.Join(Environment.NewLine, PostLogoutRedirectUris);
        set => PostLogoutRedirectUris = ParseList(value);
    }

    public string AllowedCorsOriginsText
    {
        get => string.Join(Environment.NewLine, AllowedCorsOrigins);
        set => AllowedCorsOrigins = ParseList(value);
    }

    public string AllowedScopesText
    {
        get => string.Join(" ", AllowedScopes);
        set => AllowedScopes = value.Split(new[] { ' ', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
    }

    public string NewSecretValue { get; set; } = string.Empty;
    public string NewSecretDescription { get; set; } = "Domyślny sekret";

    private static List<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new();
        return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
    }
}
