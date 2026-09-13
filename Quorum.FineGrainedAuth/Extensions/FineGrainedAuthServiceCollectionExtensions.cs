using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quorum.FineGrainedAuth.AuthZen.Data;
using Quorum.FineGrainedAuth.AuthZen.Services.PDP;
using Quorum.FineGrainedAuth.AuthZen.Services.PIP;
using Quorum.FineGrainedAuth.AuthZen.Services.PEP;
using Quorum.FineGrainedAuth.AuthZen.Stores;
using Quorum.FineGrainedAuth.OpenFGA.Services;
using Quorum.FineGrainedAuth.OpenFGA.Adapters;

namespace Quorum.FineGrainedAuth.Extensions;

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
        return services;
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
}
