using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quorum.FineGrainedAuth.AuthZen.Controllers;
using Quorum.FineGrainedAuth.AuthZen.Data;
using Quorum.FineGrainedAuth.AuthZen.Services.PDP;
using Quorum.FineGrainedAuth.AuthZen.Services.PIP;
using Quorum.FineGrainedAuth.AuthZen.Services.PEP;
using Quorum.FineGrainedAuth.AuthZen.Stores;
using Quorum.FineGrainedAuth.OpenFGA.Adapters;
using Quorum.FineGrainedAuth.OpenFGA.Models;
using Quorum.FineGrainedAuth.OpenFGA.Services;

namespace Quorum.FineGrainedAuth.Extensions;

/// <summary>
/// Opcje konfiguracyjne dla pakietu Fine-Grained Authorization (AuthZEN &amp; OpenFGA).
/// </summary>
public class FineGrainedAuthOptions
{
    /// <summary>
    /// Konfiguracja klienta i połączenia z OpenFGA (port 8080).
    /// </summary>
    public OpenFgaOptions OpenFga { get; set; } = new();

    /// <summary>
    /// Opcjonalna konfiguracja bazy danych dla zasad AuthZEN PAP.
    /// Jeśli null, automatycznie używane jest domyślne SQLite ('authzen_policies.db') lub istniejący kontekst.
    /// </summary>
    public Action<DbContextOptionsBuilder>? DbContext { get; set; }
}

public static class FineGrainedAuthServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje dedykowany kontekst bazy danych AuthZEN dla silnika PDP i magazynu zasad PAP.
    /// Domyślnie konfiguruje lokalny plik bazy SQLite ('authzen_policies.db').
    /// </summary>
    public static IServiceCollection AddAuthZenDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.AddDbContext<AuthZenDbContext>(configureOptions);
        }
        else
        {
            services.AddDbContext<AuthZenDbContext>(options =>
            {
                options.UseSqlite("Data Source=authzen_policies.db");
            });
        }

        services.AddScoped<IAuthZenDbContext>(sp => sp.GetRequiredService<AuthZenDbContext>());
        return services;
    }

    /// <summary>
    /// Rejestruje istniejący kontekst bazy danych aplikacji implementujący interfejs <see cref="IAuthZenDbContext"/>.
    /// </summary>
    public static IServiceCollection AddAuthZenDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext, IAuthZenDbContext
    {
        services.AddScoped<IAuthZenDbContext>(sp => sp.GetRequiredService<TContext>());
        return services;
    }

    /// <summary>
    /// Rejestruje usługi autoryzacji AuthZEN (silnik PDP, PIP dla tożsamości użytkowników oraz magazyn zasad PAP).
    /// Automatycznie zapewnia rejestrację IAuthZenDbContext, zapobiegając błędom walidacji kontenera DI.
    /// </summary>
    public static IServiceCollection AddAuthZenPdp<TUser>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
        where TUser : IdentityUser, new()
    {
        // 1. Zapewnij obecność IAuthZenDbContext w kontenerze DI
        if (!services.Any(d => d.ServiceType == typeof(IAuthZenDbContext)))
        {
            if (services.Any(d => d.ServiceType == typeof(AuthZenDbContext)))
            {
                services.AddScoped<IAuthZenDbContext>(sp => sp.GetRequiredService<AuthZenDbContext>());
            }
            else
            {
                services.AddAuthZenDbContext(configureDbContext);
            }
        }

        // 2. Zarejestruj komponenty PDP, PIP i Policy Store
        services.AddScoped<IAuthZenPolicyInformationPoint, AuthZenIdentityPipService<TUser>>();
        services.AddScoped<IAuthZenPolicyDecisionPoint, AuthZenPdpEngine>();
        services.AddScoped<IAuthZenPolicyStore, EfAuthZenPolicyStore>();

        // 3. Automatycznie zarejestruj kontrolery AuthZEN w ApplicationPartManager
        services.AddControllers()
            .AddApplicationPart(typeof(AdminAuthZenPoliciesController).Assembly);

        return services;
    }

    /// <summary>
    /// Rejestruje kontrolery AuthZEN (Admin PAP /admin/authzen/policies oraz silnik ewaluacji PDP)
    /// w konfiguracji MVC nadrzędnego projektu, zapewniając ich wykrycie przez routing ASP.NET Core.
    /// </summary>
    public static IMvcBuilder AddAuthZenControllers(this IServiceCollection services)
    {
        return services.AddControllers()
            .AddApplicationPart(typeof(AdminAuthZenPoliciesController).Assembly);
    }

    /// <summary>
    /// Rejestruje klienta PEP (Policy Enforcement Point) standardu AuthZEN dla proxy, gateway i middleware.
    /// </summary>
    public static IServiceCollection AddAuthZenPep(this IServiceCollection services, Action<HttpClient>? configureClient = null)
    {
        var builder = services.AddHttpClient<IAuthZenPepClient, AuthZenPepClient>();
        if (configureClient != null)
        {
            builder.ConfigureHttpClient(configureClient);
        }
        return services;
    }

    /// <summary>
    /// Rejestruje klienta OpenFGA (Google Zanzibar model), adapter AuthZEN-do-OpenFGA oraz magazyn reguł CRUD.
    /// </summary>
    public static IServiceCollection AddOpenFga(
        this IServiceCollection services,
        Action<OpenFgaOptions>? configureOptions = null,
        Action<HttpClient>? configureHttp = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddSingleton<IOpenFgaClient, OpenFgaClient>();
        services.AddScoped<AuthZenToOpenFgaAdapter>();
        services.AddSingleton<IAuthZenOpenFgaStore, AuthZenOpenFgaStore>();
        return services;
    }

    /// <summary>
    /// Rejestruje kompletny pakiet Fine-Grained Authorization (AuthZEN PDP/PEP + OpenFGA).
    /// </summary>
    public static IServiceCollection AddFineGrainedAuth<TUser>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureDbContext = null,
        Action<OpenFgaOptions>? configureOpenFga = null)
        where TUser : IdentityUser, new()
    {
        services.AddAuthZenPdp<TUser>(configureDbContext);
        services.AddAuthZenPep();
        services.AddOpenFga(configureOpenFga);
        return services;
    }

    /// <summary>
    /// Rejestruje kompletny pakiet Fine-Grained Authorization z obiektem konfiguracyjnym opcji.
    /// </summary>
    public static IServiceCollection AddFineGrainedAuth<TUser>(
        this IServiceCollection services,
        Action<FineGrainedAuthOptions> configure)
        where TUser : IdentityUser, new()
    {
        var options = new FineGrainedAuthOptions();
        configure(options);

        services.AddAuthZenPdp<TUser>(options.DbContext);
        services.AddAuthZenPep();
        services.AddOpenFga(opt =>
        {
            opt.ApiUrl = options.OpenFga.ApiUrl;
            opt.StoreId = options.OpenFga.StoreId;
            opt.DefaultAuthorizationModelId = options.OpenFga.DefaultAuthorizationModelId;
            opt.ApiToken = options.OpenFga.ApiToken;
        });
        return services;
    }
}
