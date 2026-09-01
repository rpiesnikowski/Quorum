using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.Backend.AdminUI.Options;
using Quorum.Backend.AdminUI.Services.EntityFramework;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Radzen;

namespace Quorum.Backend.AdminUI.Extensions;

public static class AdminUiServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje usługi bazowe dla panelu Blazor AdminUI (Radzen, HttpClient, opcje).
    /// </summary>
    public static IServiceCollection AddQuorumAdminUI<TUser>(
        this IServiceCollection services,
        Action<AdminUiOptions>? configureOptions = null)
        where TUser : IdentityUser, new()
    {
        var options = new AdminUiOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        // Rejestracja serwisów komponentów Radzen Blazor (DataGrid, DialogService, NotificationService, TooltipService, ContextMenuService)
        services.AddRadzenComponents();
        services.AddHttpClient();
        
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
            authOptions.AddPolicy("RequireAdminRole", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

            authOptions.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

            authOptions.AddPolicy(options.PolicyName, policy =>
            {
                policy.RequireAuthenticatedUser();
                if (!string.IsNullOrEmpty(options.RequiredRole))
                {
                    policy.RequireRole(options.RequiredRole);
                }
            });
        });

        services.AddCascadingAuthenticationState();
        
        return services;
    }

    /// <summary>
    /// Rejestruje domyślną implementację magazynu Entity Framework Core dla wszystkich sekcji CRUD w panelu AdminUI.
    /// </summary>
    public static IServiceCollection AddQuorumAdminUIEntityFrameworkStore<TUser>(
        this IServiceCollection services)
        where TUser : IdentityUser, new()
    {
        services.TryAddScoped<IAdminUserStore, EfAdminUserStore<TUser>>();
        services.TryAddScoped<IAdminClientStore, EfAdminClientStore>();
        services.TryAddScoped<IAdminApiScopeStore, EfAdminApiScopeStore>();
        services.TryAddScoped<IAdminIdentityResourceStore, EfAdminIdentityResourceStore>();
        services.TryAddScoped<IAdminFederationStore, EfAdminFederationStore>();
        services.TryAddScoped<IAdminGatewayStore, EfAdminGatewayStore>();
        services.TryAddScoped<IAdminGrantStore, EfAdminGrantStore>();
        services.TryAddScoped<IAdminDashboardStore, EfAdminDashboardStore<TUser>>();
        services.TryAddSingleton<IGatewayNotificationService, NullGatewayNotificationService>();

        return services;
    }

    // Aliasy dla zgodności wstecznej
    public static IServiceCollection AddQuorumAdminUI2<TUser>(
        this IServiceCollection services,
        Action<AdminUiOptions>? configureOptions = null)
        where TUser : IdentityUser, new()
        => AddQuorumAdminUI<TUser>(services, configureOptions);

    public static IServiceCollection AddQuorumAdminUI2EntityFrameworkStore<TUser>(
        this IServiceCollection services)
        where TUser : IdentityUser, new()
        => AddQuorumAdminUIEntityFrameworkStore<TUser>(services);
}
