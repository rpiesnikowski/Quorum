using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.Backend.AdminUI2.Options;
using Quorum.Backend.AdminUI2.Services.EntityFramework;
using Quorum.Backend.AdminUI2.Services.Interfaces;
using Radzen;

namespace Quorum.Backend.AdminUI2.Extensions;

public static class AdminUi2ServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje usługi bazowe dla panelu Blazor AdminUI 2 (Radzen, HttpClient, opcje).
    /// </summary>
    public static IServiceCollection AddQuorumAdminUI2<TUser>(
        this IServiceCollection services,
        Action<AdminUiOptions2>? configureOptions = null)
        where TUser : IdentityUser, new()
    {
        var options = new AdminUiOptions2();
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

        services.AddCascadingAuthenticationState();
        
        return services;
    }

    /// <summary>
    /// Rejestruje domyślną implementację magazynu Entity Framework Core dla wszystkich sekcji CRUD w panelu AdminUI 2.
    /// </summary>
    public static IServiceCollection AddQuorumAdminUI2EntityFrameworkStore<TUser>(
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

        return services;
    }
}
