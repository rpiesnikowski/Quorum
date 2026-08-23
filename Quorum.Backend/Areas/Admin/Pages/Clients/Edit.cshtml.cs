using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Open.IdentityServer.Models;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.Areas.Admin.Pages.Clients;

public class EditModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public EditModel(ConfigurationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public ClientInputModel Input { get; set; } = new();

    public class ClientInputModel
    {
        public int Id { get; set; }

        // --- Podstawowe ---
        [Required(ErrorMessage = "Pole Client ID jest wymagane")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pole Nazwa Klienta jest wymagane")]
        public string ClientName { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;
        public string ProtocolType { get; set; } = "oidc";
        public string? ClientUri { get; set; }
        public string? LogoUri { get; set; }

        // --- Uwierzytelnianie & Sekrety ---
        public bool RequireClientSecret { get; set; } = true;
        public string? NewSecretValue { get; set; }
        public string NewSecretType { get; set; } = "SharedSecret";
        public string? NewSecretDescription { get; set; }
        public DateTime? NewSecretExpiration { get; set; }
        public List<ExistingSecretModel> ExistingSecrets { get; set; } = new();

        // --- Przepływy & PKCE ---
        [Required(ErrorMessage = "Wymagany jest przynajmniej jeden dozwolony typ przepływu")]
        public string AllowedGrantTypes { get; set; } = "authorization_code";
        public bool RequirePkce { get; set; } = true;
        public bool AllowPlainTextPkce { get; set; } = false;
        public bool AllowAccessTokensViaBrowser { get; set; } = false;
        public bool RequireRequestObject { get; set; } = false;

        // --- Przekierowania & Wylogowanie ---
        public string? RedirectUris { get; set; }
        public string? PostLogoutRedirectUris { get; set; }
        public string? AllowedCorsOrigins { get; set; }
        public string? FrontChannelLogoutUri { get; set; }
        public bool FrontChannelLogoutSessionRequired { get; set; } = true;
        public string? BackChannelLogoutUri { get; set; }
        public bool BackChannelLogoutSessionRequired { get; set; } = true;

        // --- Zakresy & Zgoda (Scopes & Consent) ---
        public string? AllowedScopes { get; set; }
        public bool RequireConsent { get; set; } = false;
        public bool AllowRememberConsent { get; set; } = true;
        public int? ConsentLifetime { get; set; }

        // --- Tokeny & Czasy Życia ---
        public bool AllowOfflineAccess { get; set; } = true;
        public int IdentityTokenLifetime { get; set; } = 300;
        public int AccessTokenLifetime { get; set; } = 3600;
        public int AuthorizationCodeLifetime { get; set; } = 300;
        public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;
        public int SlidingRefreshTokenLifetime { get; set; } = 1296000;
        public int RefreshTokenUsage { get; set; } = 1; // 1 = ReUse, 0 = OneTime
        public int RefreshTokenExpiration { get; set; } = 1; // 1 = Absolute, 0 = Sliding
        public bool UpdateAccessTokenClaimsOnRefresh { get; set; } = false;
        public int AccessTokenType { get; set; } = 0; // 0 = Jwt, 1 = Reference
        public string? AllowedIdentityTokenSigningAlgorithms { get; set; }

        // --- Claims & Dostawcy ---
        public bool AlwaysIncludeUserClaimsInIdToken { get; set; } = false;
        public bool AlwaysSendClientClaims { get; set; } = false;
        public string? ClientClaimsPrefix { get; set; } = "client_";
        public string? PairWiseSubjectSalt { get; set; }
        public bool IncludeJwtId { get; set; } = true;
        public bool EnableLocalLogin { get; set; } = true;
        public string? IdentityProviderRestrictions { get; set; }
        public int? UserSsoLifetime { get; set; }
        public string? UserCodeType { get; set; }
        public int DeviceCodeLifetime { get; set; } = 300;

        // Statyczne Claims przypisane do Klienta
        public List<ExistingClaimModel> ExistingClaims { get; set; } = new();
        public string? NewClaimType { get; set; }
        public string? NewClaimValue { get; set; }
        public string? NewClaimValueType { get; set; } = "http://www.w3.org/2001/XMLSchema#string";
    }

    public class ExistingSecretModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = "SharedSecret";
        public string? Description { get; set; }
        public DateTime? Expiration { get; set; }
        public string ValuePreview { get; set; } = string.Empty;
        public bool Delete { get; set; } = false;
    }

    public class ExistingClaimModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ValueType { get; set; }
        public bool Delete { get; set; } = false;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Include(x => x.RedirectUris)
            .Include(x => x.PostLogoutRedirectUris)
            .Include(x => x.AllowedScopes)
            .Include(x => x.ClientSecrets)
            .Include(x => x.Claims)
            .Include(x => x.IdentityProviderRestrictions)
            .Include(x => x.AllowedCorsOrigins)
            .Include(x => x.Properties)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = $"Nie znaleziono klienta o ID {id}.";
            return RedirectToPage("Index");
        }

        var secrets = entity.ClientSecrets ?? new List<Open.IdentityServer.EntityFramework.Entities.ClientSecret>();

        Input = new ClientInputModel
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            ClientName = entity.ClientName,
            Description = entity.Description,
            Enabled = entity.Enabled,
            ProtocolType = entity.ProtocolType ?? "oidc",
            ClientUri = entity.ClientUri,
            LogoUri = entity.LogoUri,

            // Jeśli klient nie ma sekretów, uznajemy że w modelu RequireClientSecret jest false
            RequireClientSecret = (secrets.Count == 0) ? false : entity.RequireClientSecret,
            ExistingSecrets = secrets.Select(s => new ExistingSecretModel
            {
                Id = s.Id,
                Type = s.Type,
                Description = s.Description,
                Expiration = s.Expiration,
                ValuePreview = s.Value.Length > 16 ? s.Value.Substring(0, 10) + "..." : s.Value
            }).ToList(),

            AllowedGrantTypes = string.Join(" ", entity.AllowedGrantTypes.Select(g => g.GrantType)),
            RequirePkce = entity.RequirePkce,
            AllowPlainTextPkce = entity.AllowPlainTextPkce,
            AllowAccessTokensViaBrowser = entity.AllowAccessTokensViaBrowser,
            RequireRequestObject = entity.RequireRequestObject,

            RedirectUris = string.Join("\n", entity.RedirectUris.Select(r => r.RedirectUri)),
            PostLogoutRedirectUris = string.Join("\n", entity.PostLogoutRedirectUris.Select(p => p.PostLogoutRedirectUri)),
            AllowedCorsOrigins = string.Join("\n", entity.AllowedCorsOrigins.Select(c => c.Origin)),
            FrontChannelLogoutUri = entity.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = entity.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = entity.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = entity.BackChannelLogoutSessionRequired,

            AllowedScopes = string.Join(" ", entity.AllowedScopes.Select(s => s.Scope)),
            RequireConsent = entity.RequireConsent,
            AllowRememberConsent = entity.AllowRememberConsent,
            ConsentLifetime = entity.ConsentLifetime,

            AllowOfflineAccess = entity.AllowOfflineAccess,
            IdentityTokenLifetime = entity.IdentityTokenLifetime,
            AccessTokenLifetime = entity.AccessTokenLifetime,
            AuthorizationCodeLifetime = entity.AuthorizationCodeLifetime,
            AbsoluteRefreshTokenLifetime = entity.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = entity.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = entity.RefreshTokenUsage,
            RefreshTokenExpiration = entity.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = entity.UpdateAccessTokenClaimsOnRefresh,
            AccessTokenType = entity.AccessTokenType,
            AllowedIdentityTokenSigningAlgorithms = entity.AllowedIdentityTokenSigningAlgorithms,

            AlwaysIncludeUserClaimsInIdToken = entity.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = entity.AlwaysSendClientClaims,
            ClientClaimsPrefix = entity.ClientClaimsPrefix,
            PairWiseSubjectSalt = entity.PairWiseSubjectSalt,
            IncludeJwtId = entity.IncludeJwtId,
            EnableLocalLogin = entity.EnableLocalLogin,
            IdentityProviderRestrictions = string.Join(" ", entity.IdentityProviderRestrictions.Select(i => i.Provider)),
            UserSsoLifetime = entity.UserSsoLifetime,
            UserCodeType = entity.UserCodeType,
            DeviceCodeLifetime = entity.DeviceCodeLifetime,

            ExistingClaims = (entity.Claims ?? new List<Open.IdentityServer.EntityFramework.Entities.ClientClaim>()).Select(c => new ExistingClaimModel
            {
                Id = c.Id,
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType
            }).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var entity = await _context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Include(x => x.RedirectUris)
            .Include(x => x.PostLogoutRedirectUris)
            .Include(x => x.AllowedScopes)
            .Include(x => x.ClientSecrets)
            .Include(x => x.Claims)
            .Include(x => x.IdentityProviderRestrictions)
            .Include(x => x.AllowedCorsOrigins)
            .Include(x => x.Properties)
            .FirstOrDefaultAsync(x => x.Id == Input.Id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "Klient nie istnieje.";
            return RedirectToPage("Index");
        }

        // --- Aktualizacja pól skalarnych ---
        entity.ClientId = Input.ClientId.Trim();
        entity.ClientName = Input.ClientName.Trim();
        entity.Description = Input.Description;
        entity.Enabled = Input.Enabled;
        entity.ProtocolType = string.IsNullOrWhiteSpace(Input.ProtocolType) ? "oidc" : Input.ProtocolType.Trim();
        entity.ClientUri = Input.ClientUri?.Trim();
        entity.LogoUri = Input.LogoUri?.Trim();

        entity.RequireClientSecret = Input.RequireClientSecret;
        entity.RequirePkce = Input.RequirePkce;
        entity.AllowPlainTextPkce = Input.AllowPlainTextPkce;
        entity.AllowAccessTokensViaBrowser = Input.AllowAccessTokensViaBrowser;
        entity.RequireRequestObject = Input.RequireRequestObject;

        entity.FrontChannelLogoutUri = Input.FrontChannelLogoutUri?.Trim();
        entity.FrontChannelLogoutSessionRequired = Input.FrontChannelLogoutSessionRequired;
        entity.BackChannelLogoutUri = Input.BackChannelLogoutUri?.Trim();
        entity.BackChannelLogoutSessionRequired = Input.BackChannelLogoutSessionRequired;

        entity.RequireConsent = Input.RequireConsent;
        entity.AllowRememberConsent = Input.AllowRememberConsent;
        entity.ConsentLifetime = Input.ConsentLifetime;

        entity.AllowOfflineAccess = Input.AllowOfflineAccess;
        entity.IdentityTokenLifetime = Input.IdentityTokenLifetime;
        entity.AccessTokenLifetime = Input.AccessTokenLifetime;
        entity.AuthorizationCodeLifetime = Input.AuthorizationCodeLifetime;
        entity.AbsoluteRefreshTokenLifetime = Input.AbsoluteRefreshTokenLifetime;
        entity.SlidingRefreshTokenLifetime = Input.SlidingRefreshTokenLifetime;
        entity.RefreshTokenUsage = Input.RefreshTokenUsage;
        entity.RefreshTokenExpiration = Input.RefreshTokenExpiration;
        entity.UpdateAccessTokenClaimsOnRefresh = Input.UpdateAccessTokenClaimsOnRefresh;
        entity.AccessTokenType = Input.AccessTokenType;
        entity.AllowedIdentityTokenSigningAlgorithms = Input.AllowedIdentityTokenSigningAlgorithms?.Trim();

        entity.AlwaysIncludeUserClaimsInIdToken = Input.AlwaysIncludeUserClaimsInIdToken;
        entity.AlwaysSendClientClaims = Input.AlwaysSendClientClaims;
        entity.ClientClaimsPrefix = Input.ClientClaimsPrefix;
        entity.PairWiseSubjectSalt = Input.PairWiseSubjectSalt;
        entity.IncludeJwtId = Input.IncludeJwtId;
        entity.EnableLocalLogin = Input.EnableLocalLogin;
        entity.UserSsoLifetime = Input.UserSsoLifetime;
        entity.UserCodeType = Input.UserCodeType?.Trim();
        entity.DeviceCodeLifetime = Input.DeviceCodeLifetime;

        // --- Obsługa kolekcji relacyjnych ---

        // 1. Sekrety (Usuwanie oznaczonych)
        if (Input.ExistingSecrets != null && Input.ExistingSecrets.Any())
        {
            var secretsToDelete = Input.ExistingSecrets.Where(s => s.Delete).Select(s => s.Id).ToList();
            if (secretsToDelete.Any())
            {
                entity.ClientSecrets.RemoveAll(s => secretsToDelete.Contains(s.Id));
            }
        }

        // Dodanie nowego sekretu jeśli wpisano wartość
        if (!string.IsNullOrWhiteSpace(Input.NewSecretValue))
        {
            var secretVal = Input.NewSecretValue.Trim();
            // Standardowo hashowanie SHA256 dla SharedSecret
            var hashedVal = (Input.NewSecretType == "SharedSecret") ? secretVal.Sha256() : secretVal;

            entity.ClientSecrets.Add(new Open.IdentityServer.EntityFramework.Entities.ClientSecret
            {
                Type = Input.NewSecretType,
                Value = hashedVal,
                Description = Input.NewSecretDescription,
                Expiration = Input.NewSecretExpiration,
                Created = DateTime.UtcNow
            });

            // Jeśli dodano sekret, a RequireClientSecret było false, ustawiamy na true
            entity.RequireClientSecret = true;
        }
        else if (entity.ClientSecrets.Count == 0)
        {
            // Jeśli klient nie ma sekretów, wymuszamy RequireClientSecret = false
            entity.RequireClientSecret = false;
        }

        // 2. AllowedGrantTypes
        entity.AllowedGrantTypes.Clear();
        if (!string.IsNullOrWhiteSpace(Input.AllowedGrantTypes))
        {
            var grants = Input.AllowedGrantTypes.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var grant in grants)
            {
                entity.AllowedGrantTypes.Add(new Open.IdentityServer.EntityFramework.Entities.ClientGrantType
                {
                    GrantType = grant
                });
            }
        }

        // 3. AllowedScopes
        entity.AllowedScopes.Clear();
        if (!string.IsNullOrWhiteSpace(Input.AllowedScopes))
        {
            var scopes = Input.AllowedScopes.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var scope in scopes)
            {
                entity.AllowedScopes.Add(new Open.IdentityServer.EntityFramework.Entities.ClientScope
                {
                    Scope = scope
                });
            }
        }

        // 4. RedirectUris
        entity.RedirectUris.Clear();
        if (!string.IsNullOrWhiteSpace(Input.RedirectUris))
        {
            var uris = Input.RedirectUris.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var uri in uris)
            {
                entity.RedirectUris.Add(new Open.IdentityServer.EntityFramework.Entities.ClientRedirectUri
                {
                    RedirectUri = uri
                });
            }
        }

        // 5. PostLogoutRedirectUris
        entity.PostLogoutRedirectUris.Clear();
        if (!string.IsNullOrWhiteSpace(Input.PostLogoutRedirectUris))
        {
            var postUris = Input.PostLogoutRedirectUris.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var uri in postUris)
            {
                entity.PostLogoutRedirectUris.Add(new Open.IdentityServer.EntityFramework.Entities.ClientPostLogoutRedirectUri
                {
                    PostLogoutRedirectUri = uri
                });
            }
        }

        // 6. AllowedCorsOrigins
        entity.AllowedCorsOrigins.Clear();
        if (!string.IsNullOrWhiteSpace(Input.AllowedCorsOrigins))
        {
            var origins = Input.AllowedCorsOrigins.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var origin in origins)
            {
                entity.AllowedCorsOrigins.Add(new Open.IdentityServer.EntityFramework.Entities.ClientCorsOrigin
                {
                    Origin = origin
                });
            }
        }

        // 7. IdentityProviderRestrictions
        entity.IdentityProviderRestrictions.Clear();
        if (!string.IsNullOrWhiteSpace(Input.IdentityProviderRestrictions))
        {
            var idps = Input.IdentityProviderRestrictions.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
            foreach (var idp in idps)
            {
                entity.IdentityProviderRestrictions.Add(new Open.IdentityServer.EntityFramework.Entities.ClientIdPRestriction
                {
                    Provider = idp
                });
            }
        }

        // 8. Claims Klienta (Client Claims)
        if (Input.ExistingClaims != null && Input.ExistingClaims.Any())
        {
            var claimsToDelete = Input.ExistingClaims.Where(c => c.Delete).Select(c => c.Id).ToList();
            if (claimsToDelete.Any())
            {
                entity.Claims.RemoveAll(c => claimsToDelete.Contains(c.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(Input.NewClaimType) && !string.IsNullOrWhiteSpace(Input.NewClaimValue))
        {
            entity.Claims.Add(new Open.IdentityServer.EntityFramework.Entities.ClientClaim
            {
                Type = Input.NewClaimType.Trim(),
                Value = Input.NewClaimValue.Trim(),
                ValueType = string.IsNullOrWhiteSpace(Input.NewClaimValueType) ? "http://www.w3.org/2001/XMLSchema#string" : Input.NewClaimValueType.Trim()
            });
        }

        entity.Updated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Klient '{entity.ClientId}' został pomyślnie zaktualizowany.";
        return RedirectToPage("Index");
    }
}
