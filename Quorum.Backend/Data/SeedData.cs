using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.Models;

namespace Quorum.Backend.Data;

public static class SeedData
{
    private static async Task EnsureTablesCreatedAsync(DbContext context)
    {
        var databaseCreator = context.Database.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
        if (databaseCreator != null)
        {
            if (!await databaseCreator.ExistsAsync())
            {
                await databaseCreator.CreateAsync();
            }
            try
            {
                await databaseCreator.CreateTablesAsync();
            }
            catch
            {
                // Ignorujemy błędy, jeśli tabele dla danego kontekstu zostały już wcześniej utworzone
            }
        }
    }

    public static async Task EnsureSeedDataAsync(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        // 1. Zapewnienie struktury bazy danych dla wszystkich DbContextów
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await EnsureTablesCreatedAsync(appDb);

        var configDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await EnsureTablesCreatedAsync(configDb);

        var persistedGrantDb = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
        await EnsureTablesCreatedAsync(persistedGrantDb);

        // 2. Utworzenie ról Identity
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Admin", "User", "Manager"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 3. Utworzenie domyślnego konta Administratora
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@identityserver.local",
                EmailConfirmed = true,
                FullName = "Administrator Systemu"
            };

            var result = await userManager.CreateAsync(adminUser, "Pass123$");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // 4. Utworzenie konta standardowego użytkownika testowego
        var testUser = await userManager.FindByNameAsync("jan.kowalski");
        if (testUser == null)
        {
            testUser = new ApplicationUser
            {
                UserName = "jan.kowalski",
                Email = "jan.kowalski@example.com",
                EmailConfirmed = true,
                FullName = "Jan Kowalski"
            };

            var result = await userManager.CreateAsync(testUser, "Pass123$");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(testUser, "User");
            }
        }

        // 5. Seedowanie Zasobów Tożsamości (IdentityResources)
        if (!await configDb.IdentityResources.AnyAsync())
        {
            configDb.IdentityResources.AddRange(
                new IdentityResources.OpenId().ToEntity(),
                new IdentityResources.Profile().ToEntity(),
                new IdentityResources.Email().ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 6. Seedowanie Zakresów API (ApiScopes)
        if (!await configDb.ApiScopes.AnyAsync())
        {
            configDb.ApiScopes.AddRange(
                new ApiScope("api1", "Dostęp do API Produkty i Zamówienia").ToEntity(),
                new ApiScope("api.read", "Tylko odczyt danych API").ToEntity(),
                new ApiScope("api.write", "Zapis i modyfikacja danych API").ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 7. Seedowanie Przykładowych Klientów OIDC / OAuth2
        if (!await configDb.Clients.AnyAsync())
        {
            configDb.Clients.AddRange(
                // Klient Maszyna-Maszyna (Client Credentials Flow)
                new Client
                {
                    ClientId = "m2m.client",
                    ClientName = "Usługa w tle (Machine-to-Machine)",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    ClientSecrets = { new Secret("secret".Sha256()) },
                    AllowedScopes = { "api1", "api.read", "api.write" }
                }.ToEntity(),

                // Klient Interaktywny (Authorization Code Flow + PKCE)
                new Client
                {
                    ClientId = "interactive.mvc",
                    ClientName = "Aplikacja Webowa ASP.NET Core MVC / SPA",
                    AllowedGrantTypes = GrantTypes.Code,
                    RequirePkce = true,
                    RequireClientSecret = false,
                    RedirectUris = { "https://localhost:5002/signin-oidc" },
                    PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },
                    AllowedScopes = { "openid", "profile", "email", "api1" },
                    AllowOfflineAccess = true // Obsługa Refresh Tokens
                }.ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 8. Seedowanie Dynamicznych Dostawców Tożsamości OIDC (Dynamic External Providers)
        if (!await appDb.FederationProviders.AnyAsync())
        {
            appDb.FederationProviders.AddRange(
                // 1. Microsoft Entra ID (Azure Active Directory)
                new OidcFederationProvider
                {
                    Id = Guid.NewGuid().ToString(),
                    Scheme = "entra-id",
                    DisplayName = "Microsoft Entra ID",
                    Authority = "https://login.microsoftonline.com/organizations/v2.0",
                    ClientId = "00000000-0000-0000-0000-000000000001",
                    ClientSecret = "entra-sample-client-secret",
                    ResponseType = "code",
                    Scope = "openid profile email",
                    CallbackPath = "/signin-oidc-entra",
                    SignedOutCallbackPath = "/signout-callback-oidc",
                    UsePkce = true,
                    GetClaimsFromUserInfoEndpoint = true,
                    SaveTokens = true,
                    IsEnabled = true,
                    AutoProvisionUsers = true,
                    DefaultRole = "User",
                    IconType = "microsoft",
                    ButtonColor = "#0078D4",
                    Prompt = "select_account"
                },

                // 2. Azure AD B2C (Customer Identity and Access Management)
                new OidcFederationProvider
                {
                    Id = Guid.NewGuid().ToString(),
                    Scheme = "azure-b2c",
                    DisplayName = "Azure AD B2C (CIAM)",
                    Authority = "https://mytenant.b2clogin.com/mytenant.onmicrosoft.com/b2c_1_susi/v2.0/",
                    ClientId = "00000000-0000-0000-0000-000000000002",
                    ClientSecret = "b2c-sample-client-secret",
                    ResponseType = "code",
                    Scope = "openid profile email",
                    CallbackPath = "/signin-oidc-b2c",
                    SignedOutCallbackPath = "/signout-callback-oidc",
                    UsePkce = true,
                    GetClaimsFromUserInfoEndpoint = true,
                    SaveTokens = true,
                    IsEnabled = true,
                    AutoProvisionUsers = true,
                    DefaultRole = "User",
                    IconType = "azure",
                    ButtonColor = "#0089D6",
                    AdditionalParametersJson = "{\"p\": \"b2c_1_susi\"}"
                },

                // 3. Google Workspace / Google Accounts OIDC
                new OidcFederationProvider
                {
                    Id = Guid.NewGuid().ToString(),
                    Scheme = "google-oidc",
                    DisplayName = "Google Workspace",
                    Authority = "https://accounts.google.com",
                    ClientId = "000000000000-samplegoogleclientid.apps.googleusercontent.com",
                    ClientSecret = "GOCSPX-SampleGoogleClientSecret",
                    ResponseType = "code",
                    Scope = "openid profile email",
                    CallbackPath = "/signin-oidc-google",
                    SignedOutCallbackPath = "/signout-callback-oidc",
                    UsePkce = true,
                    GetClaimsFromUserInfoEndpoint = true,
                    SaveTokens = true,
                    IsEnabled = true,
                    AutoProvisionUsers = true,
                    DefaultRole = "User",
                    IconType = "google",
                    ButtonColor = "#4285F4",
                    Prompt = "select_account"
                }
            );
            await appDb.SaveChangesAsync();
        }
    }
}
