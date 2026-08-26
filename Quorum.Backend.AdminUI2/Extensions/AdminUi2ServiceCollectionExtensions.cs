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
        Action<AdminUi2Options>? configureOptions = null)
        where TUser : IdentityUser, new()
    {
        var options = new AdminUi2Options();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Rejestracja serwisów komponentów Radzen Blazor (DataGrid, DialogService, NotificationService, TooltipService, ContextMenuService)
        services.AddRadzenComponents();
        services.AddHttpClient();

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
