using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.AdminUI.Data;
using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.Services;

public class DynamicOidcService : IDynamicOidcService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly ILogger<DynamicOidcService> _logger;

    public DynamicOidcService(
        ApplicationDbContext db,
        IAuthenticationSchemeProvider schemeProvider,
        ILogger<DynamicOidcService> logger)
    {
        _db = db;
        _schemeProvider = schemeProvider;
        _logger = logger;
    }

    public async Task<List<OidcFederationProvider>> GetActiveFederationsAsync()
    {
        return await _db.FederationProviders
            .AsNoTracking()
            .Where(f => f.IsEnabled)
            .OrderBy(f => f.DisplayName)
            .ToListAsync();
    }

    public async Task<OidcFederationProvider?> GetFederationBySchemeAsync(string scheme)
    {
        return await _db.FederationProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Scheme.ToLower() == scheme.ToLower());
    }

    public async Task ReloadFederationSchemesAsync()
    {
        if (_schemeProvider is DynamicAuthenticationSchemeProvider dynamicProvider)
        {
            dynamicProvider.ClearAllDynamicSchemes();
            var active = await GetActiveFederationsAsync();
            foreach (var fed in active)
            {
                dynamicProvider.RefreshDynamicScheme(fed);
            }
            _logger.LogInformation("Przeładowano {Count} dynamicznych federacji OIDC w pamięci serwera", active.Count);
        }
    }

    public void InvalidateScheme(string scheme)
    {
        if (_schemeProvider is DynamicAuthenticationSchemeProvider dynamicProvider)
        {
            dynamicProvider.RemoveDynamicScheme(scheme);
        }
    }
}
