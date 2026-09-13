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
    /// Rejestruje usługi autoryzacji AuthZEN (silnik PDP, PIP dla tożsamości użytkowników oraz magazyn zasad PAP).
    /// </summary>
    public static IServiceCollection AddAuthZenPdp<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, new()
    {
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
    public static IServiceCollection AddFineGrainedAuth<TUser>(this IServiceCollection services)
        where TUser : IdentityUser, new()
    {
        services.AddAuthZenPdp<TUser>();
        services.AddAuthZenPep();
        services.AddOpenFga();
        return services;
    }
}
