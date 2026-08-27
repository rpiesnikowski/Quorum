using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminClientStore : IAdminClientStore
{
    private readonly ConfigurationDbContext _context;

    public EfAdminClientStore(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ClientAdminModel>> GetClientsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Clients
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.ClientId.ToLower().Contains(s) || (c.ClientName != null && c.ClientName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(c => c.ClientId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var models = entities.Select(MapToModel).ToList();
        return new PagedResult<ClientAdminModel>(models, totalCount, page, pageSize);
    }

    public async Task<ClientAdminModel?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Clients
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.ClientSecrets)
            .Include(c => c.Claims)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<(bool Success, string? Error)> CreateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Clients.AnyAsync(c => c.ClientId == model.ClientId, cancellationToken);
        if (exists)
        {
            return (false, $"Klient o identyfikatorze '{model.ClientId}' już istnieje.");
        }

        var entity = new Client
        {
            ClientId = model.ClientId,
            ClientName = model.ClientName,
            Description = model.Description,
            ClientUri = model.ClientUri,
            LogoUri = model.LogoUri,
            Enabled = model.Enabled,
            RequireClientSecret = model.RequireClientSecret,
            RequirePkce = model.RequirePkce,
            AllowPlainTextPkce = model.AllowPlainTextPkce,
            RequireConsent = model.RequireConsent,
            AllowRememberConsent = model.AllowRememberConsent,
            AlwaysIncludeUserClaimsInIdToken = model.AlwaysIncludeUserClaimsInIdToken,
            AllowOfflineAccess = model.AllowOfflineAccess,
            AccessTokenLifetime = model.AccessTokenLifetime,
            IdentityTokenLifetime = model.IdentityTokenLifetime,
            AuthorizationCodeLifetime = model.AuthorizationCodeLifetime,
            SlidingRefreshTokenLifetime = model.SlidingRefreshTokenLifetime,
            AbsoluteRefreshTokenLifetime = model.AbsoluteRefreshTokenLifetime,
            ProtocolType = model.ProtocolType
        };

        if (model.AllowedGrantTypes != null)
        {
            foreach (var gt in model.AllowedGrantTypes)
            {
                entity.AllowedGrantTypes.Add(new ClientGrantType { GrantType = gt });
            }
        }

        if (model.AllowedScopes != null)
        {
            foreach (var scope in model.AllowedScopes)
            {
                entity.AllowedScopes.Add(new ClientScope { Scope = scope });
            }
        }

        if (model.RedirectUris != null)
        {
            foreach (var uri in model.RedirectUris)
            {
                entity.RedirectUris.Add(new ClientRedirectUri { RedirectUri = uri });
            }
        }

        if (model.PostLogoutRedirectUris != null)
        {
            foreach (var uri in model.PostLogoutRedirectUris)
            {
                entity.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = uri });
            }
        }

        if (model.AllowedCorsOrigins != null)
        {
            foreach (var origin in model.AllowedCorsOrigins)
            {
                entity.AllowedCorsOrigins.Add(new ClientCorsOrigin { Origin = origin });
            }
        }

        if (!string.IsNullOrWhiteSpace(model.NewSecretValue))
        {
            // SHA256 hash or plain secret
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.NewSecretValue));
            var hashString = Convert.ToBase64String(hashBytes);

            entity.ClientSecrets.Add(new ClientSecret
            {
                Value = hashString,
                Description = model.NewSecretDescription,
                Type = "SharedSecret",
                Created = DateTime.UtcNow
            });
        }

        _context.Clients.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateClientAsync(ClientAdminModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Clients
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.ClientSecrets)
            .Include(c => c.Claims)
            .FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Klient o ID {model.Id} nie został znaleziony.");
        }

        entity.ClientId = model.ClientId;
        entity.ClientName = model.ClientName;
        entity.Description = model.Description;
        entity.ClientUri = model.ClientUri;
        entity.LogoUri = model.LogoUri;
        entity.Enabled = model.Enabled;
        entity.RequireClientSecret = model.RequireClientSecret;
        entity.RequirePkce = model.RequirePkce;
        entity.AllowPlainTextPkce = model.AllowPlainTextPkce;
        entity.RequireConsent = model.RequireConsent;
        entity.AllowRememberConsent = model.AllowRememberConsent;
        entity.AlwaysIncludeUserClaimsInIdToken = model.AlwaysIncludeUserClaimsInIdToken;
        entity.AllowOfflineAccess = model.AllowOfflineAccess;
        entity.AccessTokenLifetime = model.AccessTokenLifetime;
        entity.IdentityTokenLifetime = model.IdentityTokenLifetime;
        entity.AuthorizationCodeLifetime = model.AuthorizationCodeLifetime;
        entity.SlidingRefreshTokenLifetime = model.SlidingRefreshTokenLifetime;
        entity.AbsoluteRefreshTokenLifetime = model.AbsoluteRefreshTokenLifetime;

        // Sync Grant Types
        entity.AllowedGrantTypes.Clear();
        foreach (var gt in model.AllowedGrantTypes ?? new())
        {
            entity.AllowedGrantTypes.Add(new ClientGrantType { GrantType = gt });
        }

        // Sync Scopes
        entity.AllowedScopes.Clear();
        foreach (var sc in model.AllowedScopes ?? new())
        {
            entity.AllowedScopes.Add(new ClientScope { Scope = sc });
        }

        // Sync Redirect URIs
        entity.RedirectUris.Clear();
        foreach (var uri in model.RedirectUris ?? new())
        {
            entity.RedirectUris.Add(new ClientRedirectUri { RedirectUri = uri });
        }

        // Sync PostLogoutRedirect URIs
        entity.PostLogoutRedirectUris.Clear();
        foreach (var uri in model.PostLogoutRedirectUris ?? new())
        {
            entity.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = uri });
        }

        // Sync CORS
        entity.AllowedCorsOrigins.Clear();
        foreach (var origin in model.AllowedCorsOrigins ?? new())
        {
            entity.AllowedCorsOrigins.Add(new ClientCorsOrigin { Origin = origin });
        }

        // Add secret if entered
        if (!string.IsNullOrWhiteSpace(model.NewSecretValue))
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.NewSecretValue));
            var hashString = Convert.ToBase64String(hashBytes);

            entity.ClientSecrets.Add(new ClientSecret
            {
                Value = hashString,
                Description = string.IsNullOrWhiteSpace(model.NewSecretDescription) ? "Nowy sekret" : model.NewSecretDescription,
                Type = "SharedSecret",
                Created = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteClientAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null) return (true, null);

        _context.Clients.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddSecretAsync(int clientId, ClientSecretModel secret, CancellationToken cancellationToken = default)
    {
        var client = await _context.Clients.Include(c => c.ClientSecrets).FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null) return (false, "Nie znaleziono klienta.");

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(secret.Value));
        var hashString = Convert.ToBase64String(hashBytes);

        client.ClientSecrets.Add(new ClientSecret
        {
            Value = hashString,
            Description = secret.Description,
            Type = secret.Type ?? "SharedSecret",
            Expiration = secret.Expiration,
            Created = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteSecretAsync(int clientId, int secretId, CancellationToken cancellationToken = default)
    {
        var client = await _context.Clients.Include(c => c.ClientSecrets).FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null) return (false, "Nie znaleziono klienta.");

        var secret = client.ClientSecrets.FirstOrDefault(s => s.Id == secretId);
        if (secret != null)
        {
            client.ClientSecrets.Remove(secret);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return (true, null);
    }

    private static ClientAdminModel MapToModel(Client entity)
    {
        return new ClientAdminModel
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            ClientName = entity.ClientName ?? string.Empty,
            Description = entity.Description,
            ClientUri = entity.ClientUri,
            LogoUri = entity.LogoUri,
            Enabled = entity.Enabled,
            RequireClientSecret = entity.RequireClientSecret,
            RequirePkce = entity.RequirePkce,
            AllowPlainTextPkce = entity.AllowPlainTextPkce,
            RequireConsent = entity.RequireConsent,
            AllowRememberConsent = entity.AllowRememberConsent,
            AlwaysIncludeUserClaimsInIdToken = entity.AlwaysIncludeUserClaimsInIdToken,
            AllowOfflineAccess = entity.AllowOfflineAccess,
            AccessTokenLifetime = entity.AccessTokenLifetime,
            IdentityTokenLifetime = entity.IdentityTokenLifetime,
            AuthorizationCodeLifetime = entity.AuthorizationCodeLifetime,
            SlidingRefreshTokenLifetime = entity.SlidingRefreshTokenLifetime,
            AbsoluteRefreshTokenLifetime = entity.AbsoluteRefreshTokenLifetime,
            ProtocolType = entity.ProtocolType ?? "oidc",
            AllowedGrantTypes = entity.AllowedGrantTypes?.Select(g => g.GrantType).ToList() ?? new(),
            AllowedScopes = entity.AllowedScopes?.Select(s => s.Scope).ToList() ?? new(),
            RedirectUris = entity.RedirectUris?.Select(r => r.RedirectUri).ToList() ?? new(),
            PostLogoutRedirectUris = entity.PostLogoutRedirectUris?.Select(r => r.PostLogoutRedirectUri).ToList() ?? new(),
            AllowedCorsOrigins = entity.AllowedCorsOrigins?.Select(o => o.Origin).ToList() ?? new(),
            ClientSecrets = entity.ClientSecrets?.Select(s => new ClientSecretModel
            {
                Id = s.Id,
                Description = s.Description ?? string.Empty,
                Value = s.Value,
                Expiration = s.Expiration,
                Type = s.Type,
                Created = s.Created
            }).ToList() ?? new(),
            Claims = entity.Claims?.Select(c => new ClientClaimModel
            {
                Id = c.Id,
                Type = c.Type,
                Value = c.Value
            }).ToList() ?? new()
        };
    }
}
