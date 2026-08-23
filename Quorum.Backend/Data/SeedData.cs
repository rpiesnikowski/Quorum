using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
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
    }
}
