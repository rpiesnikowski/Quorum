using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI2.Extensions;
using Quorum.Backend.EntityFramework;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend2.Components;
using Quorum.Backend2.Data;
using Quorum.Backend2.Services;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// 1. Obsługa nagłówków X-Forwarded-Proto / X-Forwarded-For dla Reverse Proxy (Docker/Nginx/Caddy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 2. Rejestracja bazy danych dla kont użytkowników, federacji OIDC i tras API Gateway (ApplicationDbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureDatabase<ApplicationDbContext>(builder.Configuration, typeof(Program)));

builder.Services.AddScoped<IFederationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IGatewayDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// 3. Konfiguracja ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 4. Konfiguracja ciasteczek sesyjnych i logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "Quorum.Identity2";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// 5. Dynamiczne Federacje OIDC (przeładowywanie w locie bez restartu serwera)
builder.Services.AddScoped<IDynamicOidcService, DynamicOidcService>();
builder.Services.AddSingleton<IAuthenticationSchemeProvider, DynamicAuthenticationSchemeProvider>();
builder.Services.AddTransient<OpenIdConnectHandler>();

// 6. Konfiguracja Open.IdentityServer (RSK) z Entity Framework
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
    options.EmitStaticAudienceClaim = true;

    options.UserInteraction.LoginUrl = "/Account/Login";
    options.UserInteraction.LogoutUrl = "/Account/Logout";
    options.UserInteraction.ConsentUrl = "/Consent";
    options.UserInteraction.ErrorUrl = "/Error";

    options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
})
.AddAspNetIdentity<ApplicationUser>()
.AddConfigurationStore(options =>
{
    options.ConfigureDbContext = b => b.ConfigureDatabase<ConfigurationDbContext>(builder.Configuration, typeof(Program));
})
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b => b.ConfigureDatabase<PersistedGrantDbContext>(builder.Configuration, typeof(Program));
    options.EnableTokenCleanup = true;
    options.TokenCleanupInterval = 3600;
})
.AddDeveloperSigningCredential();

// 7. Konfiguracja Blazor Interactive Server (Pure Blazor - brak stron Razor MVC)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 8. Konfiguracja Quorum AdminUI 2 (Nuget RCL oparty w 100% o Radzen)
builder.Services.AddQuorumAdminUI2<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
});

// Rejestracja abstrakcyjnej implementacji Entity Framework Core dla magazynów CRUD
builder.Services.AddQuorumAdminUI2EntityFrameworkStore<ApplicationUser>();

var app = builder.Build();

// Przetwarzanie nagłówków Proxy przed routingiem
app.UseForwardedHeaders();

// 9. Automatyczna migracja i Seedowanie danych początkowych
await SeedData.EnsureSeedDataAsync(app);

// Inicjalizacja i załadowanie dynamicznych schematów OIDC do pamięci
using (var scope = app.Services.CreateScope())
{
    var dynamicOidcService = scope.ServiceProvider.GetRequiredService<IDynamicOidcService>();
    await dynamicOidcService.ReloadFederationSchemesAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

// 10. Pipeline IdentityServer
app.UseIdentityServer();

// 11. Mapowanie komponentów Blazor (z automatycznym wykrywaniem stron i komponentów z Quorum.Backend.AdminUI2)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Quorum.Backend.AdminUI2.Components.Layout.AdminLayout).Assembly
    );

app.Run();
