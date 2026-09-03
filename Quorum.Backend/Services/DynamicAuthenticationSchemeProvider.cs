using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.Services;

/// <summary>
/// Dynamiczny dostawca schematów autentykacji ASP.NET Core dla Quorum.Backend.
/// </summary>
public class DynamicAuthenticationSchemeProvider : AuthenticationSchemeProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitorCache<OpenIdConnectOptions> _openIdConnectOptionsCache;
    private readonly ILogger<DynamicAuthenticationSchemeProvider> _logger;
    private readonly ConcurrentDictionary<string, AuthenticationScheme> _dynamicSchemes = new(StringComparer.OrdinalIgnoreCase);

    public DynamicAuthenticationSchemeProvider(
        IOptions<AuthenticationOptions> options,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitorCache<OpenIdConnectOptions> openIdConnectOptionsCache,
        ILogger<DynamicAuthenticationSchemeProvider> logger)
        : base(options)
    {
        _scopeFactory = scopeFactory;
        _openIdConnectOptionsCache = openIdConnectOptionsCache;
        _logger = logger;
    }

    public override async Task<AuthenticationScheme?> GetSchemeAsync(string name)
    {
        var scheme = await base.GetSchemeAsync(name);
        if (scheme != null)
        {
            return scheme;
        }

        if (_dynamicSchemes.TryGetValue(name, out var cachedScheme))
        {
            return cachedScheme;
        }

        var provider = await LoadProviderFromDatabaseAsync(name);
        if (provider != null && provider.IsEnabled)
        {
            return RegisterDynamicScheme(provider);
        }

        return null;
    }

    public override async Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
    {
        var baseSchemes = (await base.GetAllSchemesAsync()).ToList();
        var dynamicProviders = await LoadAllActiveProvidersAsync();

        foreach (var provider in dynamicProviders)
        {
            var dynamicScheme = RegisterDynamicScheme(provider);
            if (!baseSchemes.Any(s => string.Equals(s.Name, dynamicScheme.Name, StringComparison.OrdinalIgnoreCase)))
            {
                baseSchemes.Add(dynamicScheme);
            }
        }

        return baseSchemes;
    }

    public override async Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync()
    {
        var baseSchemes = (await base.GetRequestHandlerSchemesAsync()).ToList();
        var dynamicProviders = await LoadAllActiveProvidersAsync();

        foreach (var provider in dynamicProviders)
        {
            var dynamicScheme = RegisterDynamicScheme(provider);
            if (!baseSchemes.Any(s => string.Equals(s.Name, dynamicScheme.Name, StringComparison.OrdinalIgnoreCase)))
            {
                baseSchemes.Add(dynamicScheme);
            }
        }

        return baseSchemes;
    }

    public void RemoveDynamicScheme(string schemeName)
    {
        _dynamicSchemes.TryRemove(schemeName, out _);
        _openIdConnectOptionsCache.TryRemove(schemeName);
        _logger.LogInformation("Usunięto dynamiczny schemat OIDC: {Scheme}", schemeName);
    }

    public void RefreshDynamicScheme(OidcFederationProvider provider)
    {
        RemoveDynamicScheme(provider.Scheme);
        if (provider.IsEnabled)
        {
            RegisterDynamicScheme(provider);
        }
    }

    public void ClearAllDynamicSchemes()
    {
        foreach (var scheme in _dynamicSchemes.Keys)
        {
            _openIdConnectOptionsCache.TryRemove(scheme);
        }
        _dynamicSchemes.Clear();
    }

    private AuthenticationScheme RegisterDynamicScheme(OidcFederationProvider provider)
    {
        var schemeName = provider.Scheme;
        var displayName = provider.DisplayName;

        var scheme = new AuthenticationScheme(
            schemeName,
            displayName,
            typeof(OpenIdConnectHandler));

        _dynamicSchemes[schemeName] = scheme;

        _openIdConnectOptionsCache.GetOrAdd(schemeName, () =>
        {
            var options = new OpenIdConnectOptions
            {
                SignInScheme = IdentityConstants.ExternalScheme,
                Authority = provider.Authority,
                ClientId = provider.ClientId,
                ClientSecret = provider.ClientSecret,
                ResponseType = provider.ResponseType,
                CallbackPath = provider.CallbackPath,
                SignedOutCallbackPath = provider.SignedOutCallbackPath,
                UsePkce = provider.UsePkce,
                GetClaimsFromUserInfoEndpoint = provider.GetClaimsFromUserInfoEndpoint,
                SaveTokens = provider.SaveTokens,
                RequireHttpsMetadata = !provider.Authority.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
            };

            options.Scope.Clear();
            foreach (var scope in provider.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                options.Scope.Add(scope);
            }

            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (!string.IsNullOrEmpty(provider.Prompt))
                {
                    context.ProtocolMessage.Prompt = provider.Prompt;
                }

                if (!string.IsNullOrWhiteSpace(provider.AdditionalParametersJson))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(provider.AdditionalParametersJson);
                        if (dict != null)
                        {
                            foreach (var (k, v) in dict)
                            {
                                context.ProtocolMessage.SetParameter(k, v);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return Task.CompletedTask;
            };

            return options;
        });

        return scheme;
    }

    private async Task<OidcFederationProvider?> LoadProviderFromDatabaseAsync(string scheme)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dynamicOidcService = scope.ServiceProvider.GetService<IDynamicOidcService>();
            if (dynamicOidcService != null)
            {
                return await dynamicOidcService.GetFederationBySchemeAsync(scheme);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas ładowania federacji OIDC {Scheme} z bazy", scheme);
        }
        return null;
    }

    private async Task<List<OidcFederationProvider>> LoadAllActiveProvidersAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dynamicOidcService = scope.ServiceProvider.GetService<IDynamicOidcService>();
            if (dynamicOidcService != null)
            {
                return await dynamicOidcService.GetActiveFederationsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania aktywnych federacji OIDC z bazy");
        }
        return new List<OidcFederationProvider>();
    }
}
