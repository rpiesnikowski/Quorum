using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI2.Models;

public class FederationAdminModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Schemat uwierzytelniania (Scheme) jest wymagany.")]
    [StringLength(100, ErrorMessage = "Schemat nie może przekraczać 100 znaków.")]
    public string Scheme { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa wyświetlana (DisplayName) jest wymagana.")]
    [StringLength(200, ErrorMessage = "Nazwa wyświetlana nie może przekraczać 200 znaków.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres URL dostawcy (Authority) jest wymagany.")]
    [Url(ErrorMessage = "Podaj poprawny adres URL (np. https://accounts.google.com).")]
    public string Authority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Identyfikator aplikacji (ClientId) u dostawcy jest wymagany.")]
    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    public string ResponseType { get; set; } = "code";

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string? SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    public string Scopes { get; set; } = "openid profile email";

    public bool IsEnabled { get; set; } = true;

    public bool AutoProvisionUsers { get; set; } = true;

    public string StatusSummary => IsEnabled ? "Włączony" : "Wyłączony";
    public string ProvisioningSummary => AutoProvisionUsers ? "Auto-Provisioning" : "Tylko powiązane";

    public string? DefaultRoles { get; set; } = "User";

    public string? IconUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class DiscoveryValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public List<string> SupportedScopes { get; set; } = new();
}
