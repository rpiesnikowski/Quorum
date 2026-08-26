using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI2.Models;
using Quorum.Backend.AdminUI2.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using System.Text.Json;

namespace Quorum.Backend.AdminUI2.Services.EntityFramework;

public class EfAdminFederationStore : IAdminFederationStore
{
    private readonly IFederationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EfAdminFederationStore> _logger;

    public EfAdminFederationStore(
        IFederationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<EfAdminFederationStore> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PagedResult<FederationAdminModel>> GetProvidersAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.FederationProviders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(f => f.Scheme.ToLower().Contains(s) || f.DisplayName.ToLower().Contains(s) || f.Authority.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(f => f.IsEnabled)
            .ThenBy(f => f.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(MapToModel).ToList();
        return new PagedResult<FederationAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<FederationAdminModel?> GetProviderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var strId = id.ToString();
        var entity = await _context.FederationProviders.FirstOrDefaultAsync(f => f.Id == strId, cancellationToken);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<(bool Success, string? Error)> CreateProviderAsync(FederationAdminModel model, CancellationToken cancellationToken = default)
    {
        var scheme = model.Scheme.Trim().ToLowerInvariant();
        var exists = await _context.FederationProviders.AnyAsync(f => f.Scheme == scheme, cancellationToken);
        if (exists)
        {
            return (false, $"Federacja o schemacie '{scheme}' już istnieje.");
        }

        var entity = new OidcFederationProvider
        {
            Id = Guid.NewGuid().ToString(),
            Scheme = scheme,
            DisplayName = model.DisplayName.Trim(),
            Authority = model.Authority.Trim().TrimEnd('/'),
            ClientId = model.ClientId.Trim(),
            ClientSecret = model.ClientSecret,
            ResponseType = model.ResponseType ?? "code",
            Scope = model.Scopes ?? "openid profile email",
            CallbackPath = model.CallbackPath ?? "/signin-oidc",
            SignedOutCallbackPath = model.SignedOutCallbackPath ?? "/signout-callback-oidc",
            IsEnabled = model.IsEnabled,
            AutoProvisionUsers = model.AutoProvisionUsers,
            DefaultRole = model.DefaultRoles ?? "User",
            IconType = "openid",
            CreatedAt = DateTime.UtcNow
        };

        _context.FederationProviders.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        if (int.TryParse(entity.Id, out var parsedId))
            model.Id = parsedId;

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateProviderAsync(FederationAdminModel model, CancellationToken cancellationToken = default)
    {
        var strId = model.Id.ToString();
        var entity = await _context.FederationProviders.FirstOrDefaultAsync(f => f.Id == strId || f.Scheme == model.Scheme, cancellationToken);
        if (entity == null)
        {
            return (false, "Nie znaleziono wybranej federacji.");
        }

        entity.DisplayName = model.DisplayName.Trim();
        entity.Authority = model.Authority.Trim().TrimEnd('/');
        entity.ClientId = model.ClientId.Trim();
        if (!string.IsNullOrEmpty(model.ClientSecret))
        {
            entity.ClientSecret = model.ClientSecret;
        }
        entity.ResponseType = model.ResponseType ?? "code";
        entity.Scope = model.Scopes ?? "openid profile email";
        entity.CallbackPath = model.CallbackPath ?? "/signin-oidc";
        entity.SignedOutCallbackPath = model.SignedOutCallbackPath ?? "/signout-callback-oidc";
        entity.IsEnabled = model.IsEnabled;
        entity.AutoProvisionUsers = model.AutoProvisionUsers;
        entity.DefaultRole = model.DefaultRoles ?? "User";
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteProviderAsync(int id, CancellationToken cancellationToken = default)
    {
        var strId = id.ToString();
        var entity = await _context.FederationProviders.FirstOrDefaultAsync(f => f.Id == strId, cancellationToken);
        if (entity == null) return (true, null);

        _context.FederationProviders.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleStatusAsync(int id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var strId = id.ToString();
        var entity = await _context.FederationProviders.FirstOrDefaultAsync(f => f.Id == strId, cancellationToken);
        if (entity == null) return (false, "Nie znaleziono federacji.");

        entity.IsEnabled = isEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<DiscoveryValidationResult> TestDiscoveryAsync(string authority, CancellationToken cancellationToken = default)
    {
        var result = new DiscoveryValidationResult();
        if (string.IsNullOrWhiteSpace(authority))
        {
            result.Message = "Adres URL Authority nie może być pusty.";
            return result;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            var normalized = authority.Trim().TrimEnd('/');
            var discoUrl = normalized.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{normalized}/.well-known/openid-configuration";

            var response = await client.GetAsync(discoUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                result.Message = $"Serwer zwrócił status HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) dla adresu: {discoUrl}";
                return result;
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = json.RootElement;

            result.Issuer = root.TryGetProperty("issuer", out var iss) ? iss.GetString() : null;
            result.AuthorizationEndpoint = root.TryGetProperty("authorization_endpoint", out var auth) ? auth.GetString() : null;
            result.TokenEndpoint = root.TryGetProperty("token_endpoint", out var tok) ? tok.GetString() : null;
            result.UserInfoEndpoint = root.TryGetProperty("userinfo_endpoint", out var uinfo) ? uinfo.GetString() : null;
            result.JwksUri = root.TryGetProperty("jwks_uri", out var jwks) ? jwks.GetString() : null;

            if (root.TryGetProperty("scopes_supported", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in scopes.EnumerateArray())
                {
                    if (s.GetString() is string sVal) result.SupportedScopes.Add(sVal);
                }
            }

            result.Success = !string.IsNullOrEmpty(result.Issuer) && !string.IsNullOrEmpty(result.AuthorizationEndpoint);
            result.Message = result.Success ? "Pomyślnie zweryfikowano dokument OIDC Discovery!" : "Dokument Discovery nie zawiera wymaganych pól issuer/authorization_endpoint.";
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"Błąd podczas pobierania konfiguracji: {ex.Message}";
            return result;
        }
    }

    private static FederationAdminModel MapToModel(OidcFederationProvider f)
    {
        int.TryParse(f.Id, out var idVal);
        return new FederationAdminModel
        {
            Id = idVal,
            Scheme = f.Scheme,
            DisplayName = f.DisplayName,
            Authority = f.Authority,
            ClientId = f.ClientId,
            ClientSecret = f.ClientSecret,
            ResponseType = f.ResponseType,
            CallbackPath = f.CallbackPath,
            SignedOutCallbackPath = f.SignedOutCallbackPath,
            Scopes = f.Scope,
            IsEnabled = f.IsEnabled,
            AutoProvisionUsers = f.AutoProvisionUsers,
            DefaultRoles = f.DefaultRole,
            IconUrl = f.IconType,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
