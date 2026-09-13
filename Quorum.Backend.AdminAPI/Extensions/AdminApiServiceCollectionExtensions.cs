using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Backend.AdminAPI.Options;
using Quorum.Backend.AdminAPI.Services.Http;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Extensions;

public static class AdminApiServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje usługi Quorum Admin REST API w kontenerze Dependency Injection.
    /// </summary>
    public static IServiceCollection AddQuorumAdminApi(
        this IServiceCollection services,
        Action<AdminApiOptions>? configure = null)
    {
        var options = new AdminApiOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddControllers()
            .AddApplicationPart(typeof(AdminApiServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Rejestruje klientów HTTP (IAdminStores) łączących się z zewnętrznym lub lokalnym Quorum Admin REST API.
    /// </summary>
    public static IServiceCollection AddQuorumAdminHttpClients(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        string baseUrl = "api/admin")
    {
        services.AddHttpClient("QuorumAdminApi", configureClient);

        services.AddScoped<IAdminDashboardStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AdminHttpDashboardStore(factory.CreateClient("QuorumAdminApi"), baseUrl);
        });

        services.AddScoped<IAdminClientStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AdminHttpClientStore(factory.CreateClient("QuorumAdminApi"), baseUrl);
        });

        services.AddScoped<IAdminApiScopeStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AdminHttpApiScopeStore(factory.CreateClient("QuorumAdminApi"), baseUrl);
        });

        services.AddScoped<IAdminGatewayStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AdminHttpGatewayStore(factory.CreateClient("QuorumAdminApi"), baseUrl);
        });

        return services;
    }

    /// <summary>
    /// Mapuje trasy kontrolerów Quorum Admin REST API.
    /// </summary>
    public static IEndpointRouteBuilder MapQuorumAdminApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }
}
