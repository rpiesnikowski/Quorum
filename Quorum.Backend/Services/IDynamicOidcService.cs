using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.Services;

public interface IDynamicOidcService
{
    Task<List<OidcFederationProvider>> GetActiveFederationsAsync();
    Task<OidcFederationProvider?> GetFederationBySchemeAsync(string scheme);
    Task ReloadFederationSchemesAsync();
    void InvalidateScheme(string scheme);
}
