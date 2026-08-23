using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services;

public interface IFederationAdminService
{
    Task<List<OidcFederationProvider>> GetAllFederationsAsync();
    Task<List<OidcFederationProvider>> GetActiveFederationsAsync();
    Task<OidcFederationProvider?> GetFederationByIdAsync(string id);
    Task<OidcFederationProvider?> GetFederationBySchemeAsync(string scheme);
    Task<int> GetFederationsCountAsync();
    Task<bool> CreateFederationAsync(OidcFederationProvider provider);
    Task<bool> UpdateFederationAsync(OidcFederationProvider provider);
    Task<bool> DeleteFederationAsync(string id);
    Task<bool> ToggleFederationStatusAsync(string id);
    Task<OidcDiscoveryValidationResult> ValidateDiscoveryDocumentAsync(string authorityUrl);
}

public class OidcDiscoveryValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Issuer { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public string? EndSessionEndpoint { get; set; }
    public List<string> ScopesSupported { get; set; } = new();
    public List<string> ResponseTypesSupported { get; set; } = new();
    public List<string> SubjectTypeSupported { get; set; } = new();
}
