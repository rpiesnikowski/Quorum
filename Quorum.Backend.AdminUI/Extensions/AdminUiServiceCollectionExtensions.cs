using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.Backend.AdminUI.Options;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Extensions;

public static class AdminUiServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje usługi i strony Razor Pages panelu Quorum Admin UI w kontenerze DI dla wskazanego typu użytkownika.
    /// </summary>
    public static IServiceCollection AddQuorumAdminUI<TUser>(
        this IServiceCollection services,
        Action<AdminUiOptions>? configureOptions = null)
        where TUser : IdentityUser, new()
    {
        var options = new AdminUiOptions();
        configureOptions?.Invoke(options);

        // Rejestracja opcji w DI
        services.AddSingleton(options);

        // Rejestracja serwisu zarządzania użytkownikami dla panelu AdminUI
        services.TryAddScoped<IUserAdminService, IdentityUserAdminService<TUser>>();

        // Rejestracja klienta HTTP do walidacji endpointów OIDC Discovery (.well-known)
        services.AddHttpClient();

        // Rejestracja serwisu zarządzania dynamicznymi federacjami OIDC
        services.TryAddScoped<IFederationAdminService, IdentityFederationAdminService>();

        // Konfiguracja konwencji Razor Pages dla obszaru Admin
        services.AddRazorPages(razorOptions =>
        {
            if (options.EnableAuthorization)
            {
                razorOptions.Conventions.AuthorizeAreaFolder("Admin", options.AreaFolder, options.PolicyName);
            }
        });

        // Rejestracja domyślnej polityki opartej o rolę, jeśli nie została jeszcze zdefiniowana
        services.AddAuthorizationCore(authOptions =>
        {
            if (authOptions.GetPolicy(options.PolicyName) == null)
            {
                authOptions.AddPolicy(options.PolicyName, policy =>
                {
                    policy.RequireRole(options.RequiredRole);
                });
            }
        });

        return services;
    }

    /// <summary>
    /// Rejestruje usługi i strony Razor Pages panelu Quorum Admin UI w kontenerze DI z domyślnym typem IdentityUser.
    /// </summary>
    public static IServiceCollection AddQuorumAdminUI(
        this IServiceCollection services,
        Action<AdminUiOptions>? configureOptions = null)
    {
        return services.AddQuorumAdminUI<IdentityUser>(configureOptions);
    }

    /// <summary>
    /// Konfiguruje potok middleware dla Quorum Admin UI (pliki statyczne, routing).
    /// </summary>
    public static IApplicationBuilder UseQuorumAdminUI(this IApplicationBuilder app)
    {
        // Zapewnia obsługę Static Web Assets osadzonych w bibliotece RCL / pakiecie NuGet
        app.UseStaticFiles();
        return app;
    }
}
