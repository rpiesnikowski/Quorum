using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend2.Data;

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

        try
        {
            if (appDb.Database.IsSqlite())
            {
                await appDb.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""GatewayRouteScopes"" (
                        ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_GatewayRouteScopes"" PRIMARY KEY AUTOINCREMENT,
                        ""GatewayRouteId"" INTEGER NOT NULL,
                        ""Scope"" TEXT NOT NULL,
                        CONSTRAINT ""FK_GatewayRouteScopes_GatewayRoutes_GatewayRouteId"" FOREIGN KEY (""GatewayRouteId"") REFERENCES ""GatewayRoutes"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_GatewayRouteScopes_GatewayRouteId_Scope"" ON ""GatewayRouteScopes"" (""GatewayRouteId"", ""Scope"");
                ");
            }
        }
        catch
        {
        }

        var configDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await EnsureTablesCreatedAsync(configDb);

        var persistedGrantDb = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
        await EnsureTablesCreatedAsync(persistedGrantDb);

        // 2. Utworzenie ról Identity
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Admin", "User", "Manager", "Developer"];
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
                FullName = "Administrator Systemu Quorum 2"
            };

            var result = await userManager.CreateAsync(adminUser, "Pass123$");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Użytkownik testowy
        var demoUser = await userManager.FindByNameAsync("demouser");
        if (demoUser == null)
        {
            demoUser = new ApplicationUser
            {
                UserName = "demouser",
                Email = "demouser@identityserver.local",
                EmailConfirmed = true,
                FullName = "Użytkownik Demonstracyjny"
            };
            var result = await userManager.CreateAsync(demoUser, "Pass123$");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demoUser, "User");
            }
        }

        // 4. Inicjalizacja IdentityResources (OpenID Connect standard)
        if (!await configDb.IdentityResources.AnyAsync())
        {
            configDb.IdentityResources.AddRange(
                new IdentityResources.OpenId().ToEntity(),
                new IdentityResources.Profile().ToEntity(),
                new IdentityResources.Email().ToEntity(),
                new IdentityResources.Address().ToEntity(),
                new IdentityResources.Phone().ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 5. Inicjalizacja ApiScopes
        if (!await configDb.ApiScopes.AnyAsync())
        {
            configDb.ApiScopes.AddRange(
                new ApiScope("quorum_api", "Pełny dostęp do API Quorum").ToEntity(),
                new ApiScope("api1", "Domyślny dostęp do mikroserwisów wewnętrznych").ToEntity(),
                new ApiScope("orders.read", "Odczyt zamówień").ToEntity(),
                new ApiScope("orders.write", "Zapis i modyfikacja zamówień").ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 6. Inicjalizacja Domyślnych Klientów OAuth
        if (!await configDb.Clients.AnyAsync())
        {
            // Klient SPA Blazor / React z PKCE
            var spaClient = new Client
            {
                ClientId = "spa_client",
                ClientName = "Aplikacja SPA (Blazor / React)",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RedirectUris = { "http://localhost:3000/callback", "https://localhost:5001/signin-oidc" },
                PostLogoutRedirectUris = { "http://localhost:3000", "https://localhost:5001/signout-callback-oidc" },
                AllowedCorsOrigins = { "http://localhost:3000", "https://localhost:5001" },
                AllowedScopes = { "openid", "profile", "email", "quorum_api", "api1" },
                AllowOfflineAccess = true,
                AccessTokenLifetime = 3600
            };

            // Klient M2M (Client Credentials)
            var m2mClient = new Client
            {
                ClientId = "m2m_client",
                ClientName = "Usługa w tle (Machine-to-Machine)",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("m2m_secret_pass".Sha256()) },
                AllowedScopes = { "quorum_api", "api1" },
                AccessTokenLifetime = 7200
            };

            // Klient Web App MVC / Razor
            var webClient = new Client
            {
                ClientId = "web_app",
                ClientName = "Aplikacja Webowa Backend",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = true,
                ClientSecrets = { new Secret("web_secret_pass".Sha256()) },
                RedirectUris = { "https://localhost:5002/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },
                AllowedScopes = { "openid", "profile", "email", "quorum_api", "api1" },
                AllowOfflineAccess = true
            };

            configDb.Clients.AddRange(
                spaClient.ToEntity(),
                m2mClient.ToEntity(),
                webClient.ToEntity()
            );
            await configDb.SaveChangesAsync();
        }

        // 7. Inicjalizacja przykładowych tras API Gateway
        if (!await appDb.GatewayRoutes.AnyAsync())
        {
            var route1 = new GatewayRoute
            {
                MatchPattern = "/api/orders/.*",
                RouteName = "Serwis Zamówień",
                Description = "Routing do mikroserwisu zamówień",
                Scheme = "http",
                AddressHost = "localhost",
                AddressPort = 5005,
                AddressBasePath = "",
                AddressPath = "",
                HttpMethods = "GET,POST,PUT,DELETE",
                AllowAnonymous = false,
                RequiredScope = true,
                IsEnabled = true,
                Priority = 100,
                CreatedAt = DateTime.UtcNow
            };
            route1.Scopes.Add(new GatewayRouteScope { Scope = "orders.read" });
            route1.Scopes.Add(new GatewayRouteScope { Scope = "quorum_api" });

            var route2 = new GatewayRoute
            {
                MatchPattern = "/api/public/.*",
                RouteName = "Publiczne Endpointy",
                Description = "Dostęp publiczny bez autoryzacji tokenu",
                Scheme = "http",
                AddressHost = "localhost",
                AddressPort = 5001,
                AddressBasePath = "",
                AddressPath = "",
                HttpMethods = "GET",
                AllowAnonymous = true,
                RequiredScope = false,
                IsEnabled = true,
                Priority = 10,
                CreatedAt = DateTime.UtcNow
            };

            appDb.GatewayRoutes.AddRange(route1, route2);
            await appDb.SaveChangesAsync();
        }

        // 8. Inicjalizacja przykładowych dostawców Federacji OIDC
        if (!await appDb.FederationProviders.AnyAsync())
        {
            var microsoftFed = new OidcFederationProvider
            {
                Id = Guid.NewGuid().ToString(),
                Scheme = "microsoft",
                DisplayName = "Microsoft Entra ID (Azure AD)",
                Authority = "https://login.microsoftonline.com/common/v2.0",
                ClientId = "demo-microsoft-client-id",
                ClientSecret = "demo-microsoft-secret",
                ResponseType = "code",
                Scope = "openid profile email",
                CallbackPath = "/signin-oidc-microsoft",
                SignedOutCallbackPath = "/signout-callback-microsoft",
                IsEnabled = true,
                AutoProvisionUsers = true,
                DefaultRole = "User",
                CreatedAt = DateTime.UtcNow
            };

            var googleFed = new OidcFederationProvider
            {
                Id = Guid.NewGuid().ToString(),
                Scheme = "google",
                DisplayName = "Google Workspace & Accounts",
                Authority = "https://accounts.google.com",
                ClientId = "demo-google-client-id.apps.googleusercontent.com",
                ClientSecret = "demo-google-secret",
                ResponseType = "code",
                Scope = "openid profile email",
                CallbackPath = "/signin-oidc-google",
                SignedOutCallbackPath = "/signout-callback-google",
                IsEnabled = true,
                AutoProvisionUsers = true,
                DefaultRole = "User",
                CreatedAt = DateTime.UtcNow
            };

            appDb.FederationProviders.AddRange(microsoftFed, googleFed);
            await appDb.SaveChangesAsync();
        }
    }
}
