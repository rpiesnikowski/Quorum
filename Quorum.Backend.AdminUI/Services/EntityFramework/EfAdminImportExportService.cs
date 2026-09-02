using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminImportExportService<TUser> : IAdminImportExportService
    where TUser : IdentityUser, new()
{
    private readonly ConfigurationDbContext _configContext;
    private readonly PersistedGrantDbContext _grantContext;
    private readonly ApplicationDbContext _gatewayContext;
    private readonly IFederationDbContext _federationContext;
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<IdentityRole>? _roleManager;
    private readonly ILogger<EfAdminImportExportService<TUser>> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public EfAdminImportExportService(
        ConfigurationDbContext configContext,
        PersistedGrantDbContext grantContext,
        ApplicationDbContext gatewayContext,
        IFederationDbContext federationContext,
        UserManager<TUser> userManager,
        RoleManager<IdentityRole>? roleManager = null,
        ILogger<EfAdminImportExportService<TUser>>? logger = null)
    {
        _configContext = configContext;
        _grantContext = grantContext;
        _gatewayContext = gatewayContext;
        _federationContext = federationContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EfAdminImportExportService<TUser>>.Instance;
    }

    #region Preview & Validation

    public DataImportPreview PreviewImportJson(string json, ImportEntityType targetType)
    {
        var preview = new DataImportPreview { DetectedType = targetType };
        if (string.IsNullOrWhiteSpace(json))
        {
            preview.IsValidJson = false;
            preview.ErrorMessage = "Podana zawartość JSON jest pusta.";
            return preview;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            preview.IsValidJson = true;

            switch (targetType)
            {
                case ImportEntityType.Clients:
                    var clients = ParseList<ClientAdminModel>(json);
                    preview.DetectedCount = clients.Count;
                    preview.ItemIdentifiers = clients.Select(c => !string.IsNullOrEmpty(c.ClientId) ? c.ClientId : c.ClientName).Take(25).ToList();
                    break;

                case ImportEntityType.ApiScopes:
                    var scopes = ParseList<ApiScopeAdminModel>(json);
                    preview.DetectedCount = scopes.Count;
                    preview.ItemIdentifiers = scopes.Select(s => s.Name).Take(25).ToList();
                    break;

                case ImportEntityType.IdentityResources:
                    var idRes = ParseList<IdentityResourceAdminModel>(json);
                    preview.DetectedCount = idRes.Count;
                    preview.ItemIdentifiers = idRes.Select(r => r.Name).Take(25).ToList();
                    break;

                case ImportEntityType.Users:
                    var users = ParseList<UserAdminModel>(json);
                    preview.DetectedCount = users.Count;
                    preview.ItemIdentifiers = users.Select(u => !string.IsNullOrEmpty(u.UserName) ? u.UserName : u.Email).Take(25).ToList();
                    break;

                case ImportEntityType.Federations:
                    var feds = ParseList<FederationAdminModel>(json);
                    preview.DetectedCount = feds.Count;
                    preview.ItemIdentifiers = feds.Select(f => f.Scheme).Take(25).ToList();
                    break;

                case ImportEntityType.GatewayRoutes:
                    var routes = ParseList<GatewayRouteAdminModel>(json);
                    preview.DetectedCount = routes.Count;
                    preview.ItemIdentifiers = routes.Select(r => !string.IsNullOrEmpty(r.RouteName) ? $"{r.RouteName} ({r.MatchPattern})" : r.MatchPattern).Take(25).ToList();
                    break;

                case ImportEntityType.Grants:
                    var grants = ParseList<PersistedGrantAdminModel>(json);
                    preview.DetectedCount = grants.Count;
                    preview.ItemIdentifiers = grants.Select(g => $"{g.Key} ({g.Type})").Take(25).ToList();
                    break;

                case ImportEntityType.FullBackup:
                    var backup = JsonSerializer.Deserialize<FullSystemBackupModel>(json, JsonOptions);
                    if (backup != null)
                    {
                        var total = backup.Clients.Count + backup.ApiScopes.Count + backup.IdentityResources.Count +
                                    backup.Users.Count + backup.Federations.Count + backup.GatewayRoutes.Count;
                        preview.DetectedCount = total;
                        preview.ItemIdentifiers = new List<string>
                        {
                            $"Klienci ({backup.Clients.Count})",
                            $"Zakresy API ({backup.ApiScopes.Count})",
                            $"Zasoby Tożsamości ({backup.IdentityResources.Count})",
                            $"Użytkownicy ({backup.Users.Count})",
                            $"Dostawcy OIDC ({backup.Federations.Count})",
                            $"Trasy Gateway ({backup.GatewayRoutes.Count})"
                        };
                    }
                    break;
            }

            return preview;
        }
        catch (Exception ex)
        {
            preview.IsValidJson = false;
            preview.ErrorMessage = $"Błąd struktury JSON: {ex.Message}";
            return preview;
        }
    }

    private static List<T> ParseList<T>(string json)
    {
        var trimmed = json.Trim();
        if (trimmed.StartsWith("["))
        {
            return JsonSerializer.Deserialize<List<T>>(trimmed, JsonOptions) ?? new();
        }
        else if (trimmed.StartsWith("{"))
        {
            var single = JsonSerializer.Deserialize<T>(trimmed, JsonOptions);
            return single != null ? new List<T> { single } : new();
        }
        return new();
    }

    #endregion

    #region 1. Clients (Klienci OAuth / OIDC)

    public async Task<string> ExportClientsJsonAsync(CancellationToken cancellationToken = default)
    {
        var clients = await _configContext.Clients
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.ClientSecrets)
            .Include(c => c.Claims)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var models = clients.Select(c => new ClientAdminModel
        {
            ClientId = c.ClientId,
            ClientName = c.ClientName,
            Description = c.Description,
            ClientUri = c.ClientUri,
            LogoUri = c.LogoUri,
            Enabled = c.Enabled,
            RequireClientSecret = c.RequireClientSecret,
            RequirePkce = c.RequirePkce,
            AllowPlainTextPkce = c.AllowPlainTextPkce,
            RequireConsent = c.RequireConsent,
            AllowRememberConsent = c.AllowRememberConsent,
            AlwaysIncludeUserClaimsInIdToken = c.AlwaysIncludeUserClaimsInIdToken,
            AllowOfflineAccess = c.AllowOfflineAccess,
            AccessTokenLifetime = c.AccessTokenLifetime,
            IdentityTokenLifetime = c.IdentityTokenLifetime,
            AuthorizationCodeLifetime = c.AuthorizationCodeLifetime,
            SlidingRefreshTokenLifetime = c.SlidingRefreshTokenLifetime,
            AbsoluteRefreshTokenLifetime = c.AbsoluteRefreshTokenLifetime,
            ProtocolType = c.ProtocolType,
            AllowedGrantTypes = c.AllowedGrantTypes.Select(gt => gt.GrantType).ToList(),
            AllowedScopes = c.AllowedScopes.Select(s => s.Scope).ToList(),
            RedirectUris = c.RedirectUris.Select(r => r.RedirectUri).ToList(),
            PostLogoutRedirectUris = c.PostLogoutRedirectUris.Select(p => p.PostLogoutRedirectUri).ToList(),
            AllowedCorsOrigins = c.AllowedCorsOrigins.Select(o => o.Origin).ToList(),
            ClientSecrets = c.ClientSecrets.Select(s => new ClientSecretModel
            {
                Type = s.Type,
                Value = s.Value,
                Description = s.Description,
                Expiration = s.Expiration
            }).ToList(),
            Claims = c.Claims.Select(cl => new ClientClaimModel
            {
                Type = cl.Type,
                Value = cl.Value
            }).ToList()
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportClientsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<ClientAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji klientów w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _configContext.Clients.ToListAsync(cancellationToken);
                _configContext.Clients.RemoveRange(existingAll);
                await _configContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ClientId))
                {
                    result.Errors.Add($"Pominięto klienta bez określonego 'ClientId'.");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _configContext.Clients
                    .Include(c => c.AllowedGrantTypes)
                    .Include(c => c.AllowedScopes)
                    .Include(c => c.RedirectUris)
                    .Include(c => c.PostLogoutRedirectUris)
                    .Include(c => c.AllowedCorsOrigins)
                    .Include(c => c.ClientSecrets)
                    .Include(c => c.Claims)
                    .FirstOrDefaultAsync(c => c.ClientId.ToLower() == item.ClientId.Trim().ToLower(), cancellationToken);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejącego klienta '{item.ClientId}'.");
                        continue;
                    }

                    // Aktualizacja istniejącego
                    existing.ClientName = item.ClientName ?? item.ClientId;
                    existing.Description = item.Description;
                    existing.ClientUri = item.ClientUri;
                    existing.LogoUri = item.LogoUri;
                    existing.Enabled = item.Enabled;
                    existing.RequireClientSecret = item.RequireClientSecret;
                    existing.RequirePkce = item.RequirePkce;
                    existing.AllowPlainTextPkce = item.AllowPlainTextPkce;
                    existing.RequireConsent = item.RequireConsent;
                    existing.AllowRememberConsent = item.AllowRememberConsent;
                    existing.AlwaysIncludeUserClaimsInIdToken = item.AlwaysIncludeUserClaimsInIdToken;
                    existing.AllowOfflineAccess = item.AllowOfflineAccess;
                    existing.AccessTokenLifetime = item.AccessTokenLifetime;
                    existing.IdentityTokenLifetime = item.IdentityTokenLifetime;
                    existing.AuthorizationCodeLifetime = item.AuthorizationCodeLifetime;
                    existing.SlidingRefreshTokenLifetime = item.SlidingRefreshTokenLifetime;
                    existing.AbsoluteRefreshTokenLifetime = item.AbsoluteRefreshTokenLifetime;
                    existing.ProtocolType = string.IsNullOrEmpty(item.ProtocolType) ? "oidc" : item.ProtocolType;

                    // Sync sub-structures by name / values
                    existing.AllowedGrantTypes.Clear();
                    foreach (var gt in item.AllowedGrantTypes ?? new())
                    {
                        existing.AllowedGrantTypes.Add(new ClientGrantType { GrantType = gt.Trim() });
                    }

                    existing.AllowedScopes.Clear();
                    foreach (var sc in item.AllowedScopes ?? new())
                    {
                        existing.AllowedScopes.Add(new ClientScope { Scope = sc.Trim() });
                    }

                    existing.RedirectUris.Clear();
                    foreach (var uri in item.RedirectUris ?? new())
                    {
                        existing.RedirectUris.Add(new ClientRedirectUri { RedirectUri = uri.Trim() });
                    }

                    existing.PostLogoutRedirectUris.Clear();
                    foreach (var uri in item.PostLogoutRedirectUris ?? new())
                    {
                        existing.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = uri.Trim() });
                    }

                    existing.AllowedCorsOrigins.Clear();
                    foreach (var origin in item.AllowedCorsOrigins ?? new())
                    {
                        existing.AllowedCorsOrigins.Add(new ClientCorsOrigin { Origin = origin.Trim() });
                    }

                    if (item.ClientSecrets != null && item.ClientSecrets.Count > 0)
                    {
                        foreach (var secret in item.ClientSecrets)
                        {
                            if (!existing.ClientSecrets.Any(s => s.Value == secret.Value))
                            {
                                existing.ClientSecrets.Add(new ClientSecret
                                {
                                    Type = secret.Type ?? "SharedSecret",
                                    Value = secret.Value,
                                    Description = secret.Description,
                                    Expiration = secret.Expiration,
                                    Created = DateTime.UtcNow
                                });
                            }
                        }
                    }

                    existing.Claims.Clear();
                    foreach (var cl in item.Claims ?? new())
                    {
                        existing.Claims.Add(new ClientClaim { Type = cl.Type, Value = cl.Value });
                    }

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano klienta '{item.ClientId}'.");
                }
                else
                {
                    // Nowy klient
                    var entity = new Client
                    {
                        ClientId = item.ClientId.Trim(),
                        ClientName = item.ClientName ?? item.ClientId,
                        Description = item.Description,
                        ClientUri = item.ClientUri,
                        LogoUri = item.LogoUri,
                        Enabled = item.Enabled,
                        RequireClientSecret = item.RequireClientSecret,
                        RequirePkce = item.RequirePkce,
                        AllowPlainTextPkce = item.AllowPlainTextPkce,
                        RequireConsent = item.RequireConsent,
                        AllowRememberConsent = item.AllowRememberConsent,
                        AlwaysIncludeUserClaimsInIdToken = item.AlwaysIncludeUserClaimsInIdToken,
                        AllowOfflineAccess = item.AllowOfflineAccess,
                        AccessTokenLifetime = item.AccessTokenLifetime,
                        IdentityTokenLifetime = item.IdentityTokenLifetime,
                        AuthorizationCodeLifetime = item.AuthorizationCodeLifetime,
                        SlidingRefreshTokenLifetime = item.SlidingRefreshTokenLifetime,
                        AbsoluteRefreshTokenLifetime = item.AbsoluteRefreshTokenLifetime,
                        ProtocolType = string.IsNullOrEmpty(item.ProtocolType) ? "oidc" : item.ProtocolType
                    };

                    foreach (var gt in item.AllowedGrantTypes ?? new())
                    {
                        entity.AllowedGrantTypes.Add(new ClientGrantType { GrantType = gt.Trim() });
                    }

                    foreach (var sc in item.AllowedScopes ?? new())
                    {
                        entity.AllowedScopes.Add(new ClientScope { Scope = sc.Trim() });
                    }

                    foreach (var uri in item.RedirectUris ?? new())
                    {
                        entity.RedirectUris.Add(new ClientRedirectUri { RedirectUri = uri.Trim() });
                    }

                    foreach (var uri in item.PostLogoutRedirectUris ?? new())
                    {
                        entity.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = uri.Trim() });
                    }

                    foreach (var origin in item.AllowedCorsOrigins ?? new())
                    {
                        entity.AllowedCorsOrigins.Add(new ClientCorsOrigin { Origin = origin.Trim() });
                    }

                    foreach (var secret in item.ClientSecrets ?? new())
                    {
                        entity.ClientSecrets.Add(new ClientSecret
                        {
                            Type = secret.Type ?? "SharedSecret",
                            Value = secret.Value,
                            Description = secret.Description,
                            Expiration = secret.Expiration,
                            Created = DateTime.UtcNow
                        });
                    }

                    foreach (var cl in item.Claims ?? new())
                    {
                        entity.Claims.Add(new ClientClaim { Type = cl.Type, Value = cl.Value });
                    }

                    _configContext.Clients.Add(entity);
                    result.AddedCount++;
                    result.Messages.Add($"Utworzono klienta '{item.ClientId}'.");
                }
            }

            await _configContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano pomyślnie: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu klientów.");
            result.Success = false;
            result.Errors.Add($"Błąd importu klientów: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 2. ApiScopes (Zakresy API)

    public async Task<string> ExportApiScopesJsonAsync(CancellationToken cancellationToken = default)
    {
        var scopes = await _configContext.ApiScopes
            .Include(s => s.UserClaims)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var models = scopes.Select(s => new ApiScopeAdminModel
        {
            Name = s.Name,
            DisplayName = s.DisplayName,
            Description = s.Description,
            Required = s.Required,
            Emphasize = s.Emphasize,
            ShowInDiscoveryDocument = s.ShowInDiscoveryDocument,
            Enabled = s.Enabled,
            UserClaims = s.UserClaims.Select(c => c.Type).ToList()
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportApiScopesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<ApiScopeAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji zakresów API w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _configContext.ApiScopes.ToListAsync(cancellationToken);
                _configContext.ApiScopes.RemoveRange(existingAll);
                await _configContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    result.Errors.Add("Pominięto zakres API bez określonej nazwy ('Name').");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _configContext.ApiScopes
                    .Include(s => s.UserClaims)
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == item.Name.Trim().ToLower(), cancellationToken);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejący zakres '{item.Name}'.");
                        continue;
                    }

                    existing.DisplayName = item.DisplayName;
                    existing.Description = item.Description;
                    existing.Required = item.Required;
                    existing.Emphasize = item.Emphasize;
                    existing.ShowInDiscoveryDocument = item.ShowInDiscoveryDocument;
                    existing.Enabled = item.Enabled;

                    // Sync user claims by name
                    existing.UserClaims.Clear();
                    foreach (var claimType in item.UserClaims ?? new())
                    {
                        existing.UserClaims.Add(new ApiScopeClaim { Type = claimType.Trim() });
                    }

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano zakres '{item.Name}'.");
                }
                else
                {
                    var entity = new ApiScope
                    {
                        Name = item.Name.Trim(),
                        DisplayName = item.DisplayName,
                        Description = item.Description,
                        Required = item.Required,
                        Emphasize = item.Emphasize,
                        ShowInDiscoveryDocument = item.ShowInDiscoveryDocument,
                        Enabled = item.Enabled
                    };

                    foreach (var claimType in item.UserClaims ?? new())
                    {
                        entity.UserClaims.Add(new ApiScopeClaim { Type = claimType.Trim() });
                    }

                    _configContext.ApiScopes.Add(entity);
                    result.AddedCount++;
                    result.Messages.Add($"Utworzono zakres '{item.Name}'.");
                }
            }

            await _configContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano pomyślnie: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu zakresów API.");
            result.Success = false;
            result.Errors.Add($"Błąd importu zakresów API: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 3. IdentityResources (Zasoby Tożsamości)

    public async Task<string> ExportIdentityResourcesJsonAsync(CancellationToken cancellationToken = default)
    {
        var resources = await _configContext.IdentityResources
            .Include(r => r.UserClaims)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var models = resources.Select(r => new IdentityResourceAdminModel
        {
            Name = r.Name,
            DisplayName = r.DisplayName,
            Description = r.Description,
            Required = r.Required,
            Emphasize = r.Emphasize,
            ShowInDiscoveryDocument = r.ShowInDiscoveryDocument,
            Enabled = r.Enabled,
            UserClaims = r.UserClaims.Select(c => c.Type).ToList()
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportIdentityResourcesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<IdentityResourceAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji zasobów tożsamości w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _configContext.IdentityResources.ToListAsync(cancellationToken);
                _configContext.IdentityResources.RemoveRange(existingAll);
                await _configContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    result.Errors.Add("Pominięto zasób tożsamości bez określonej nazwy ('Name').");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _configContext.IdentityResources
                    .Include(r => r.UserClaims)
                    .FirstOrDefaultAsync(r => r.Name.ToLower() == item.Name.Trim().ToLower(), cancellationToken);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejący zasób tożsamości '{item.Name}'.");
                        continue;
                    }

                    existing.DisplayName = item.DisplayName;
                    existing.Description = item.Description;
                    existing.Required = item.Required;
                    existing.Emphasize = item.Emphasize;
                    existing.ShowInDiscoveryDocument = item.ShowInDiscoveryDocument;
                    existing.Enabled = item.Enabled;

                    // Sync user claims by name
                    existing.UserClaims.Clear();
                    foreach (var claimType in item.UserClaims ?? new())
                    {
                        existing.UserClaims.Add(new IdentityResourceClaim { Type = claimType.Trim() });
                    }

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano zasób tożsamości '{item.Name}'.");
                }
                else
                {
                    var entity = new IdentityResource
                    {
                        Name = item.Name.Trim(),
                        DisplayName = item.DisplayName,
                        Description = item.Description,
                        Required = item.Required,
                        Emphasize = item.Emphasize,
                        ShowInDiscoveryDocument = item.ShowInDiscoveryDocument,
                        Enabled = item.Enabled
                    };

                    foreach (var claimType in item.UserClaims ?? new())
                    {
                        entity.UserClaims.Add(new IdentityResourceClaim { Type = claimType.Trim() });
                    }

                    _configContext.IdentityResources.Add(entity);
                    result.AddedCount++;
                    result.Messages.Add($"Utworzono zasób tożsamości '{item.Name}'.");
                }
            }

            await _configContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano pomyślnie: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu zasobów tożsamości.");
            result.Success = false;
            result.Errors.Add($"Błąd importu zasobów tożsamości: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 4. Users (Użytkownicy i Role)

    public async Task<string> ExportUsersJsonAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var models = new List<UserAdminModel>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var claims = await _userManager.GetClaimsAsync(u);

            models.Add(new UserAdminModel
            {
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                PhoneNumberConfirmed = u.PhoneNumberConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = u.LockoutEnd,
                AccessFailedCount = u.AccessFailedCount,
                Roles = roles.ToList(),
                Claims = claims.Select(c => new UserClaimModel { Type = c.Type, Value = c.Value }).ToList()
            });
        }

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportUsersJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<UserAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji użytkowników w pliku JSON.");
                result.Success = false;
                return result;
            }

            // Upewnij się, że wszystkie role z importu istnieją w systemie
            if (_roleManager != null)
            {
                var allRolesToEnsure = items.SelectMany(u => u.Roles ?? new()).Distinct();
                foreach (var roleName in allRolesToEnsure)
                {
                    if (!string.IsNullOrWhiteSpace(roleName) && !await _roleManager.RoleExistsAsync(roleName.Trim()))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(roleName.Trim()));
                    }
                }
            }

            foreach (var item in items)
            {
                var username = !string.IsNullOrWhiteSpace(item.UserName) ? item.UserName.Trim() : item.Email?.Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    result.Errors.Add("Pominięto użytkownika bez nazwy użytkownika i adresu e-mail.");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _userManager.FindByNameAsync(username) ??
                               (!string.IsNullOrWhiteSpace(item.Email) ? await _userManager.FindByEmailAsync(item.Email.Trim()) : null);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejącego użytkownika '{username}'.");
                        continue;
                    }

                    existing.Email = !string.IsNullOrWhiteSpace(item.Email) ? item.Email.Trim() : existing.Email;
                    existing.EmailConfirmed = item.EmailConfirmed;
                    existing.PhoneNumber = item.PhoneNumber;
                    existing.PhoneNumberConfirmed = item.PhoneNumberConfirmed;
                    existing.TwoFactorEnabled = item.TwoFactorEnabled;
                    existing.LockoutEnabled = item.LockoutEnabled;
                    if (item.LockoutEnd.HasValue)
                    {
                        existing.LockoutEnd = item.LockoutEnd;
                    }

                    await _userManager.UpdateAsync(existing);

                    // Sync roles by name
                    if (item.Roles != null)
                    {
                        var currentRoles = await _userManager.GetRolesAsync(existing);
                        var targetRoles = item.Roles.Select(r => r.Trim()).Distinct().ToList();

                        var toRemove = currentRoles.Except(targetRoles).ToList();
                        var toAdd = targetRoles.Except(currentRoles).ToList();

                        if (toRemove.Count > 0) await _userManager.RemoveFromRolesAsync(existing, toRemove);
                        if (toAdd.Count > 0) await _userManager.AddToRolesAsync(existing, toAdd);
                    }

                    // Sync claims by type/value
                    if (item.Claims != null)
                    {
                        var currentClaims = await _userManager.GetClaimsAsync(existing);
                        await _userManager.RemoveClaimsAsync(existing, currentClaims);

                        var newClaims = item.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
                        if (newClaims.Count > 0) await _userManager.AddClaimsAsync(existing, newClaims);
                    }

                    // Optional password update
                    if (!string.IsNullOrWhiteSpace(item.NewPassword))
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(existing);
                        await _userManager.ResetPasswordAsync(existing, token, item.NewPassword);
                    }

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano użytkownika '{username}'.");
                }
                else
                {
                    var newUser = new TUser
                    {
                        UserName = username,
                        Email = item.Email ?? username,
                        EmailConfirmed = item.EmailConfirmed,
                        PhoneNumber = item.PhoneNumber,
                        PhoneNumberConfirmed = item.PhoneNumberConfirmed,
                        TwoFactorEnabled = item.TwoFactorEnabled,
                        LockoutEnabled = item.LockoutEnabled
                    };

                    IdentityResult createRes;
                    if (!string.IsNullOrWhiteSpace(item.NewPassword))
                    {
                        createRes = await _userManager.CreateAsync(newUser, item.NewPassword);
                    }
                    else
                    {
                        createRes = await _userManager.CreateAsync(newUser);
                    }

                    if (createRes.Succeeded)
                    {
                        if (item.Roles != null && item.Roles.Count > 0)
                        {
                            await _userManager.AddToRolesAsync(newUser, item.Roles.Select(r => r.Trim()));
                        }

                        if (item.Claims != null && item.Claims.Count > 0)
                        {
                            var claims = item.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
                            await _userManager.AddClaimsAsync(newUser, claims);
                        }

                        result.AddedCount++;
                        result.Messages.Add($"Utworzono użytkownika '{username}'.");
                    }
                    else
                    {
                        result.Errors.Add($"Błąd tworzenia użytkownika '{username}': {string.Join(", ", createRes.Errors.Select(e => e.Description))}");
                        result.SkippedCount++;
                    }
                }
            }

            result.SummaryMessage = $"Zaimportowano użytkowników: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu użytkowników.");
            result.Success = false;
            result.Errors.Add($"Błąd importu użytkowników: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 5. Federations (Dostawcy OIDC SSO)

    public async Task<string> ExportFederationsJsonAsync(CancellationToken cancellationToken = default)
    {
        var federations = await _federationContext.FederationProviders.AsNoTracking().ToListAsync(cancellationToken);
        var models = federations.Select(f => new FederationAdminModel
        {
            Scheme = f.Scheme,
            DisplayName = f.DisplayName,
            Authority = f.Authority,
            ClientId = f.ClientId,
            ClientSecret = f.ClientSecret,
            ResponseType = f.ResponseType,
            CallbackPath = f.CallbackPath,
            SignedOutCallbackPath = f.SignedOutCallbackPath,
            Scopes = f.Scopes,
            IsEnabled = f.IsEnabled,
            AutoProvisionUsers = f.AutoProvisionUsers,
            DefaultRoles = f.DefaultRoles,
            IconUrl = f.IconUrl
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportFederationsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<FederationAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji dostawców OIDC w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _federationContext.FederationProviders.ToListAsync(cancellationToken);
                _federationContext.FederationProviders.RemoveRange(existingAll);
                await _federationContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Scheme))
                {
                    result.Errors.Add("Pominięto dostawcę OIDC bez określonego schematu ('Scheme').");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _federationContext.FederationProviders
                    .FirstOrDefaultAsync(f => f.Scheme.ToLower() == item.Scheme.Trim().ToLower(), cancellationToken);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejącego dostawcę '{item.Scheme}'.");
                        continue;
                    }

                    existing.DisplayName = item.DisplayName ?? item.Scheme;
                    existing.Authority = item.Authority;
                    existing.ClientId = item.ClientId;
                    if (!string.IsNullOrEmpty(item.ClientSecret)) existing.ClientSecret = item.ClientSecret;
                    existing.ResponseType = item.ResponseType ?? "code";
                    existing.CallbackPath = item.CallbackPath ?? "/signin-oidc";
                    existing.SignedOutCallbackPath = item.SignedOutCallbackPath ?? "/signout-callback-oidc";
                    existing.Scopes = item.Scopes ?? "openid profile email";
                    existing.IsEnabled = item.IsEnabled;
                    existing.AutoProvisionUsers = item.AutoProvisionUsers;
                    existing.DefaultRoles = item.DefaultRoles ?? "User";
                    existing.IconUrl = item.IconUrl;
                    existing.UpdatedAt = DateTime.UtcNow;

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano dostawcę OIDC '{item.Scheme}'.");
                }
                else
                {
                    var entity = new FederationProvider
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Scheme = item.Scheme.Trim(),
                        DisplayName = item.DisplayName ?? item.Scheme,
                        Authority = item.Authority,
                        ClientId = item.ClientId,
                        ClientSecret = item.ClientSecret,
                        ResponseType = item.ResponseType ?? "code",
                        CallbackPath = item.CallbackPath ?? "/signin-oidc",
                        SignedOutCallbackPath = item.SignedOutCallbackPath ?? "/signout-callback-oidc",
                        Scopes = item.Scopes ?? "openid profile email",
                        IsEnabled = item.IsEnabled,
                        AutoProvisionUsers = item.AutoProvisionUsers,
                        DefaultRoles = item.DefaultRoles ?? "User",
                        IconUrl = item.IconUrl,
                        CreatedAt = DateTime.UtcNow
                    };

                    _federationContext.FederationProviders.Add(entity);
                    result.AddedCount++;
                    result.Messages.Add($"Utworzono dostawcę OIDC '{item.Scheme}'.");
                }
            }

            await _federationContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano dostawców OIDC: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu dostawców OIDC.");
            result.Success = false;
            result.Errors.Add($"Błąd importu dostawców OIDC: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 6. GatewayRoutes (Trasy API Gateway)

    public async Task<string> ExportGatewayRoutesJsonAsync(CancellationToken cancellationToken = default)
    {
        var routes = await _gatewayContext.GatewayRoutes
            .Include(r => r.Scopes)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var models = routes.Select(r => new GatewayRouteAdminModel
        {
            MatchPattern = r.MatchPattern,
            RouteName = r.RouteName,
            Description = r.Description,
            Priority = r.Priority,
            IsEnabled = r.IsEnabled,
            Scheme = r.Scheme,
            AddressHost = r.AddressHost,
            AddressPort = r.AddressPort,
            AddressBasePath = r.AddressBasePath,
            AddressPath = r.AddressPath,
            AddressQueryString = r.AddressQueryString,
            ForwardOriginalHost = r.ForwardOriginalHost,
            TimeoutSeconds = r.TimeoutSeconds,
            AllowAnonymous = r.AllowAnonymous,
            RequiredScope = r.RequiredScope,
            AuthenticationSchemes = r.AuthenticationSchemes,
            Headers = r.Headers,
            Body = r.Body,
            BodyTransformType = r.BodyTransformType,
            EnableCaching = r.EnableCaching,
            HttpMethods = r.HttpMethods,
            RequiredScopes = r.Scopes.Select(s => s.Scope).ToList()
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportGatewayRoutesJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<GatewayRouteAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji tras API Gateway w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _gatewayContext.GatewayRoutes.ToListAsync(cancellationToken);
                _gatewayContext.GatewayRoutes.RemoveRange(existingAll);
                await _gatewayContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.MatchPattern))
                {
                    result.Errors.Add("Pominięto trasę bez określonego wzorca dopasowania ('MatchPattern').");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _gatewayContext.GatewayRoutes
                    .Include(r => r.Scopes)
                    .FirstOrDefaultAsync(r => r.MatchPattern.ToLower() == item.MatchPattern.Trim().ToLower(), cancellationToken);

                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Pominięto istniejącą trasę '{item.MatchPattern}'.");
                        continue;
                    }

                    existing.RouteName = item.RouteName;
                    existing.Description = item.Description;
                    existing.Priority = item.Priority;
                    existing.IsEnabled = item.IsEnabled;
                    existing.Scheme = item.Scheme ?? "https";
                    existing.AddressHost = item.AddressHost ?? "localhost";
                    existing.AddressPort = item.AddressPort > 0 ? item.AddressPort : 443;
                    existing.AddressBasePath = item.AddressBasePath;
                    existing.AddressPath = item.AddressPath;
                    existing.AddressQueryString = item.AddressQueryString;
                    existing.ForwardOriginalHost = item.ForwardOriginalHost;
                    existing.TimeoutSeconds = item.TimeoutSeconds > 0 ? item.TimeoutSeconds : 30;
                    existing.AllowAnonymous = item.AllowAnonymous;
                    existing.RequiredScope = item.RequiredScope;
                    existing.AuthenticationSchemes = item.AuthenticationSchemes ?? "Bearer";
                    existing.Headers = item.Headers;
                    existing.Body = item.Body;
                    existing.BodyTransformType = item.BodyTransformType ?? "Fluid";
                    existing.EnableCaching = item.EnableCaching;
                    existing.HttpMethods = item.HttpMethods;

                    // Sync scopes by name
                    existing.Scopes.Clear();
                    foreach (var scopeName in item.RequiredScopes ?? new())
                    {
                        existing.Scopes.Add(new GatewayRouteScope { Scope = scopeName.Trim() });
                    }

                    result.UpdatedCount++;
                    result.Messages.Add($"Zaktualizowano trasę '{item.MatchPattern}'.");
                }
                else
                {
                    var entity = new GatewayRoute
                    {
                        MatchPattern = item.MatchPattern.Trim(),
                        RouteName = item.RouteName,
                        Description = item.Description,
                        Priority = item.Priority,
                        IsEnabled = item.IsEnabled,
                        Scheme = item.Scheme ?? "https",
                        AddressHost = item.AddressHost ?? "localhost",
                        AddressPort = item.AddressPort > 0 ? item.AddressPort : 443,
                        AddressBasePath = item.AddressBasePath,
                        AddressPath = item.AddressPath,
                        AddressQueryString = item.AddressQueryString,
                        ForwardOriginalHost = item.ForwardOriginalHost,
                        TimeoutSeconds = item.TimeoutSeconds > 0 ? item.TimeoutSeconds : 30,
                        AllowAnonymous = item.AllowAnonymous,
                        RequiredScope = item.RequiredScope,
                        AuthenticationSchemes = item.AuthenticationSchemes ?? "Bearer",
                        Headers = item.Headers,
                        Body = item.Body,
                        BodyTransformType = item.BodyTransformType ?? "Fluid",
                        EnableCaching = item.EnableCaching,
                        HttpMethods = item.HttpMethods,
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var scopeName in item.RequiredScopes ?? new())
                    {
                        entity.Scopes.Add(new GatewayRouteScope { Scope = scopeName.Trim() });
                    }

                    _gatewayContext.GatewayRoutes.Add(entity);
                    result.AddedCount++;
                    result.Messages.Add($"Utworzono trasę '{item.MatchPattern}'.");
                }
            }

            await _gatewayContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano trasy API Gateway: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu tras API Gateway.");
            result.Success = false;
            result.Errors.Add($"Błąd importu tras API Gateway: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 7. PersistedGrants (Aktywne Granty i Tokeny)

    public async Task<string> ExportGrantsJsonAsync(CancellationToken cancellationToken = default)
    {
        var grants = await _grantContext.PersistedGrants.AsNoTracking().ToListAsync(cancellationToken);
        var models = grants.Select(g => new PersistedGrantAdminModel
        {
            Key = g.Key,
            Type = g.Type,
            SubjectId = g.SubjectId,
            SessionId = g.SessionId,
            ClientId = g.ClientId,
            Description = g.Description,
            CreationTime = g.CreationTime,
            Expiration = g.Expiration,
            ConsumedTime = g.ConsumedTime,
            Data = g.Data
        }).ToList();

        return JsonSerializer.Serialize(models, JsonOptions);
    }

    public async Task<DataImportResult> ImportGrantsJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var items = ParseList<PersistedGrantAdminModel>(json);
            if (items.Count == 0)
            {
                result.Errors.Add("Nie odnaleziono poprawnych definicji grantów w pliku JSON.");
                result.Success = false;
                return result;
            }

            if (strategy == ImportStrategy.ReplaceAll)
            {
                var existingAll = await _grantContext.PersistedGrants.ToListAsync(cancellationToken);
                _grantContext.PersistedGrants.RemoveRange(existingAll);
                await _grantContext.SaveChangesAsync(cancellationToken);
                result.DeletedCount = existingAll.Count;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    result.Errors.Add("Pominięto grant bez określonego klucza ('Key').");
                    result.SkippedCount++;
                    continue;
                }

                var existing = await _grantContext.PersistedGrants.FirstOrDefaultAsync(g => g.Key == item.Key.Trim(), cancellationToken);
                if (existing != null)
                {
                    if (strategy == ImportStrategy.AddNewOnly)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    existing.Type = item.Type ?? existing.Type;
                    existing.SubjectId = item.SubjectId;
                    existing.SessionId = item.SessionId;
                    existing.ClientId = item.ClientId ?? existing.ClientId;
                    existing.Description = item.Description;
                    existing.CreationTime = item.CreationTime != default ? item.CreationTime : existing.CreationTime;
                    existing.Expiration = item.Expiration;
                    existing.ConsumedTime = item.ConsumedTime;
                    existing.Data = item.Data ?? existing.Data;

                    result.UpdatedCount++;
                }
                else
                {
                    var entity = new PersistedGrant
                    {
                        Key = item.Key.Trim(),
                        Type = item.Type ?? "user_grant",
                        SubjectId = item.SubjectId,
                        SessionId = item.SessionId,
                        ClientId = item.ClientId ?? string.Empty,
                        Description = item.Description,
                        CreationTime = item.CreationTime != default ? item.CreationTime : DateTime.UtcNow,
                        Expiration = item.Expiration,
                        ConsumedTime = item.ConsumedTime,
                        Data = item.Data ?? string.Empty
                    };

                    _grantContext.PersistedGrants.Add(entity);
                    result.AddedCount++;
                }
            }

            await _grantContext.SaveChangesAsync(cancellationToken);
            result.SummaryMessage = $"Zaimportowano granty: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu grantów.");
            result.Success = false;
            result.Errors.Add($"Błąd importu grantów: {ex.Message}");
            return result;
        }
    }

    #endregion

    #region 8. Full System Backup & Restore

    public async Task<string> ExportFullBackupJsonAsync(CancellationToken cancellationToken = default)
    {
        var backup = new FullSystemBackupModel
        {
            Version = "1.0",
            ExportedAt = DateTime.UtcNow,
            System = "Quorum Identity Server & API Gateway"
        };

        // 1. ApiScopes
        var scopesJson = await ExportApiScopesJsonAsync(cancellationToken);
        backup.ApiScopes = JsonSerializer.Deserialize<List<ApiScopeAdminModel>>(scopesJson, JsonOptions) ?? new();

        // 2. IdentityResources
        var idResJson = await ExportIdentityResourcesJsonAsync(cancellationToken);
        backup.IdentityResources = JsonSerializer.Deserialize<List<IdentityResourceAdminModel>>(idResJson, JsonOptions) ?? new();

        // 3. Clients
        var clientsJson = await ExportClientsJsonAsync(cancellationToken);
        backup.Clients = JsonSerializer.Deserialize<List<ClientAdminModel>>(clientsJson, JsonOptions) ?? new();

        // 4. Users
        var usersJson = await ExportUsersJsonAsync(cancellationToken);
        backup.Users = JsonSerializer.Deserialize<List<UserAdminModel>>(usersJson, JsonOptions) ?? new();

        // 5. Federations
        var fedsJson = await ExportFederationsJsonAsync(cancellationToken);
        backup.Federations = JsonSerializer.Deserialize<List<FederationAdminModel>>(fedsJson, JsonOptions) ?? new();

        // 6. GatewayRoutes
        var routesJson = await ExportGatewayRoutesJsonAsync(cancellationToken);
        backup.GatewayRoutes = JsonSerializer.Deserialize<List<GatewayRouteAdminModel>>(routesJson, JsonOptions) ?? new();

        return JsonSerializer.Serialize(backup, JsonOptions);
    }

    public async Task<DataImportResult> ImportFullBackupJsonAsync(string json, ImportStrategy strategy = ImportStrategy.Upsert, CancellationToken cancellationToken = default)
    {
        var result = new DataImportResult();
        try
        {
            var backup = JsonSerializer.Deserialize<FullSystemBackupModel>(json, JsonOptions);
            if (backup == null)
            {
                result.Errors.Add("Niepoprawny format pliku pełnej kopii zapasowej JSON.");
                result.Success = false;
                return result;
            }

            // 1. ApiScopes
            if (backup.ApiScopes.Count > 0)
            {
                var r = await ImportApiScopesJsonAsync(JsonSerializer.Serialize(backup.ApiScopes, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 2. IdentityResources
            if (backup.IdentityResources.Count > 0)
            {
                var r = await ImportIdentityResourcesJsonAsync(JsonSerializer.Serialize(backup.IdentityResources, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 3. Clients
            if (backup.Clients.Count > 0)
            {
                var r = await ImportClientsJsonAsync(JsonSerializer.Serialize(backup.Clients, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 4. Users
            if (backup.Users.Count > 0)
            {
                var r = await ImportUsersJsonAsync(JsonSerializer.Serialize(backup.Users, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 5. Federations
            if (backup.Federations.Count > 0)
            {
                var r = await ImportFederationsJsonAsync(JsonSerializer.Serialize(backup.Federations, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 6. GatewayRoutes
            if (backup.GatewayRoutes.Count > 0)
            {
                var r = await ImportGatewayRoutesJsonAsync(JsonSerializer.Serialize(backup.GatewayRoutes, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            // 7. PersistedGrants
            if (backup.PersistedGrants != null && backup.PersistedGrants.Count > 0)
            {
                var r = await ImportGrantsJsonAsync(JsonSerializer.Serialize(backup.PersistedGrants, JsonOptions), strategy, cancellationToken);
                result.AddedCount += r.AddedCount;
                result.UpdatedCount += r.UpdatedCount;
                result.SkippedCount += r.SkippedCount;
                result.Errors.AddRange(r.Errors);
                result.Messages.AddRange(r.Messages);
            }

            result.SummaryMessage = $"Przywrócono pełną konfigurację: dodano {result.AddedCount}, zaktualizowano {result.UpdatedCount}, pominięto {result.SkippedCount}.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas importu pełnej kopii zapasowej.");
            result.Success = false;
            result.Errors.Add($"Błąd importu kopii zapasowej: {ex.Message}");
            return result;
        }
    }

    #endregion
}
