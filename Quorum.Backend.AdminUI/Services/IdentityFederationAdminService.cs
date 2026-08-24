using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Services;

public class IdentityFederationAdminService : IFederationAdminService
{
    private readonly IFederationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IdentityFederationAdminService> _logger;
    private readonly Action<string>? _onSchemeChanged;

    public IdentityFederationAdminService(
        IFederationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<IdentityFederationAdminService> logger,
        Action<string>? onSchemeChanged = null)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _onSchemeChanged = onSchemeChanged;
    }

    public async Task<List<OidcFederationProvider>> GetAllFederationsAsync()
    {
        return await _dbContext.FederationProviders
            .AsNoTracking()
            .OrderByDescending(f => f.IsEnabled)
            .ThenBy(f => f.DisplayName)
            .ToListAsync();
    }

    public async Task<List<OidcFederationProvider>> GetActiveFederationsAsync()
    {
        return await _dbContext.FederationProviders
            .AsNoTracking()
            .Where(f => f.IsEnabled)
            .OrderBy(f => f.DisplayName)
            .ToListAsync();
    }

    public async Task<OidcFederationProvider?> GetFederationByIdAsync(string id)
    {
        return await _dbContext.FederationProviders
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<OidcFederationProvider?> GetFederationBySchemeAsync(string scheme)
    {
        return await _dbContext.FederationProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Scheme.ToLower() == scheme.ToLower());
    }

    public async Task<int> GetFederationsCountAsync()
    {
        return await _dbContext.FederationProviders.CountAsync();
    }

    public async Task<bool> CreateFederationAsync(OidcFederationProvider provider)
    {
        try
        {
            provider.Scheme = provider.Scheme.Trim().ToLowerInvariant();
            provider.Authority = provider.Authority.Trim().TrimEnd('/');
            provider.DisplayName = provider.DisplayName.Trim();
            provider.CreatedAt = DateTime.UtcNow;

            var exists = await _dbContext.FederationProviders
                .AnyAsync(f => f.Scheme == provider.Scheme);
            if (exists)
            {
                return false;
            }

            _dbContext.FederationProviders.Add(provider);
            await _dbContext.SaveChangesAsync();

            _onSchemeChanged?.Invoke(provider.Scheme);
            _logger.LogInformation("Zarejestrowano nową dynamiczną federację OIDC: {Scheme} ({DisplayName})", provider.Scheme, provider.DisplayName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas tworzenia federacji OIDC {Scheme}", provider.Scheme);
            return false;
        }
    }

    public async Task<bool> UpdateFederationAsync(OidcFederationProvider provider)
    {
        try
        {
            var existing = await _dbContext.FederationProviders
                .FirstOrDefaultAsync(f => f.Id == provider.Id);
            if (existing == null)
            {
                return false;
            }

            existing.DisplayName = provider.DisplayName.Trim();
            existing.Authority = provider.Authority.Trim().TrimEnd('/');
            existing.ClientId = provider.ClientId.Trim();
            if (!string.IsNullOrEmpty(provider.ClientSecret))
            {
                existing.ClientSecret = provider.ClientSecret;
            }
            existing.ResponseType = provider.ResponseType;
            existing.Scope = provider.Scope;
            existing.CallbackPath = provider.CallbackPath;
            existing.SignedOutCallbackPath = provider.SignedOutCallbackPath;
            existing.UsePkce = provider.UsePkce;
            existing.GetClaimsFromUserInfoEndpoint = provider.GetClaimsFromUserInfoEndpoint;
            existing.SaveTokens = provider.SaveTokens;
            existing.IsEnabled = provider.IsEnabled;
            existing.AutoProvisionUsers = provider.AutoProvisionUsers;
            existing.DefaultRole = provider.DefaultRole;
            existing.IconType = provider.IconType;
            existing.ButtonColor = provider.ButtonColor;
            existing.Prompt = provider.Prompt;
            existing.AdditionalParametersJson = provider.AdditionalParametersJson;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _onSchemeChanged?.Invoke(existing.Scheme);
            _logger.LogInformation("Zaktualizowano dynamiczną federację OIDC: {Scheme}", existing.Scheme);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas aktualizacji federacji OIDC {Id}", provider.Id);
            return false;
        }
    }

    public async Task<bool> DeleteFederationAsync(string id)
    {
        try
        {
            var existing = await _dbContext.FederationProviders
                .FirstOrDefaultAsync(f => f.Id == id);
            if (existing == null)
            {
                return false;
            }

            var scheme = existing.Scheme;
            _dbContext.FederationProviders.Remove(existing);
            await _dbContext.SaveChangesAsync();

            _onSchemeChanged?.Invoke(scheme);
            _logger.LogInformation("Usunięto dynamiczną federację OIDC: {Scheme}", scheme);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania federacji OIDC {Id}", id);
            return false;
        }
    }

    public async Task<bool> ToggleFederationStatusAsync(string id)
    {
        try
        {
            var existing = await _dbContext.FederationProviders
                .FirstOrDefaultAsync(f => f.Id == id);
            if (existing == null)
            {
                return false;
            }

            existing.IsEnabled = !existing.IsEnabled;
            existing.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _onSchemeChanged?.Invoke(existing.Scheme);
            _logger.LogInformation("Zmieniono status aktywności federacji OIDC {Scheme} na {Status}", existing.Scheme, existing.IsEnabled);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przełączania statusu federacji OIDC {Id}", id);
            return false;
        }
    }

    public async Task<OidcDiscoveryValidationResult> ValidateDiscoveryDocumentAsync(string authorityUrl)
    {
        var result = new OidcDiscoveryValidationResult();
        if (string.IsNullOrWhiteSpace(authorityUrl))
        {
            result.ErrorMessage = "Adres Authority URL nie może być pusty.";
            return result;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            var normalizedUrl = authorityUrl.Trim().TrimEnd('/');
            var discoveryUrl = normalizedUrl.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase)
                ? normalizedUrl
                : $"{normalizedUrl}/.well-known/openid-configuration";

            var response = await client.GetAsync(discoveryUrl);
            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"Serwer tożsamości zwrócił status HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) przy próbie pobrania: {discoveryUrl}";
                return result;
            }

            using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDoc.RootElement;

            result.Issuer = root.TryGetProperty("issuer", out var issuer) ? issuer.GetString() : null;
            result.AuthorizationEndpoint = root.TryGetProperty("authorization_endpoint", out var authEp) ? authEp.GetString() : null;
            result.TokenEndpoint = root.TryGetProperty("token_endpoint", out var tokenEp) ? tokenEp.GetString() : null;
            result.UserInfoEndpoint = root.TryGetProperty("userinfo_endpoint", out var userEp) ? userEp.GetString() : null;
            result.JwksUri = root.TryGetProperty("jwks_uri", out var jwks) ? jwks.GetString() : null;
            result.EndSessionEndpoint = root.TryGetProperty("end_session_endpoint", out var endEp) ? endEp.GetString() : null;

            if (root.TryGetProperty("scopes_supported", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in scopes.EnumerateArray())
                {
                    if (s.GetString() is string sVal) result.ScopesSupported.Add(sVal);
                }
            }

            if (root.TryGetProperty("response_types_supported", out var rTypes) && rTypes.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rTypes.EnumerateArray())
                {
                    if (r.GetString() is string rVal) result.ResponseTypesSupported.Add(rVal);
                }
            }

            result.IsValid = !string.IsNullOrEmpty(result.Issuer) && !string.IsNullOrEmpty(result.AuthorizationEndpoint);
            if (!result.IsValid)
            {
                result.ErrorMessage = "Dokument Discovery został pobrany, lecz brakuje w nim kluczowych pól: issuer lub authorization_endpoint.";
            }

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Błąd połączenia z serwerem Authority: {ex.Message}";
            return result;
        }
    }
}
