using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.Backend.AdminUI.Options;
using Quorum.Backend.AdminUI.Services;
using Radzen;

namespace Quorum.Backend.AdminUI.Extensions;

public static class AdminUiServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje usługi i strony Razor Pages panelu Quorum Admin UI w kontenerze DI dla wskazanego typu użytkownika.
    /// Konfiguruje dedykowany, odizolowany schemat uwierzytelniania dla administratorów (Sposób nr 1).
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

        // Rejestracja serwisów komponentów Radzen Blazor (DataGrid, Dialog, Notification itp.)
        services.AddRadzenComponents();

        // Rejestracja serwisu zarządzania użytkownikami dla panelu AdminUI
        services.TryAddScoped<IUserAdminService, IdentityUserAdminService<TUser>>();

        // Rejestracja klienta HTTP do walidacji endpointów OIDC Discovery (.well-known)
        services.AddHttpClient();

        // Rejestracja serwisu zarządzania dynamicznymi federacjami OIDC
        services.TryAddScoped<IFederationAdminService, IdentityFederationAdminService>();

        // Rejestracja serwisu zarządzania API Gateway
        services.TryAddScoped<IGatewayAdminService, GatewayAdminService>();

        // Rejestracja dedykowanego schematu uwierzytelniania ciasteczkowego dla administratorów
        services.AddAuthentication()
            .AddCookie(options.AuthenticationScheme, cookieOptions =>
            {
                cookieOptions.Cookie.Name = options.CookieName;
                cookieOptions.LoginPath = options.LoginPath;
                cookieOptions.LogoutPath = options.LogoutPath;
                cookieOptions.AccessDeniedPath = options.AccessDeniedPath;
                cookieOptions.ReturnUrlParameter = "returnUrl";
                cookieOptions.ExpireTimeSpan = options.ExpireTimeSpan;
                cookieOptions.SlidingExpiration = true;
                cookieOptions.Cookie.HttpOnly = true;
                cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
                cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

        // Rejestracja dedykowanej polityki autoryzacji opartej o schemat administratora i wymaganą rolę
        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy(options.PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(options.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                if (!string.IsNullOrEmpty(options.RequiredRole))
                {
                    policy.RequireRole(options.RequiredRole);
                }
            });
        });

        // Konfiguracja konwencji Razor Pages dla obszaru Admin
        services.AddRazorPages(razorOptions =>
        {
            if (options.EnableAuthorization)
            {
                // Zabezpieczenie całego obszaru /Admin polityką administratora
                razorOptions.Conventions.AuthorizeAreaFolder("Admin", options.AreaFolder, options.PolicyName);

                // Dostęp anonimowy dla dedykowanych stron uwierzytelniania AdminUI
                razorOptions.Conventions.AllowAnonymousToAreaPage("Admin", "/Account/Login");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Admin", "/Account/Logout");
                razorOptions.Conventions.AllowAnonymousToAreaPage("Admin", "/Account/AccessDenied");
            }
        });
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddRazorComponents().AddInteractiveServerComponents();
        

        return services;
    }

    /// <summary>
    /// Konfiguruje potok middleware dla Quorum Admin UI (pliki statyczne, routing).
    /// </summary>
    public static IApplicationBuilder UseQuorumAdminUI(this IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        
        // ✅ Mapowanie punktów końcowych na IEndpointRouteBuilder (endpoints)
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapStaticAssets();
            endpoints.MapRazorPages();
            endpoints.MapBlazorHub();
        });
        
        return app;
    }
}
