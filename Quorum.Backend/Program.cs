using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Data;
using Quorum.Backend.AdminUI.Extensions;
using Quorum.Backend.EntityFramework;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Obsługa nagłówków X-Forwarded-Proto / X-Forwarded-For dla Reverse Proxy (Docker/Nginx/Caddy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 2. Rejestracja bazy danych dla kont użytkowników i dynamicznych federacji (ApplicationDbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureDatabase<ApplicationDbContext>(builder.Configuration, typeof(Program)));

// Rejestracja interfejsu IFederationDbContext dla panelu AdminUI
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

// 4. Konfiguracja ciasteczek logowania i sesji (elastyczna obsługa HTTP / HTTPS w Development i Produkcji)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "Quorum.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

// 5. Konfiguracja Dynamicznych Dostawców Tożsamości OIDC (bez restartu serwera)
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
    options.UserInteraction.ErrorUrl = "/Home/Error";

    options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
})
.AddAspNetIdentity<ApplicationUser>()
// Magazyn konfiguracji w bazie danych (Klienci, Scopes, IdentityResources)
.AddConfigurationStore(options =>
{
    options.ConfigureDbContext = b => b.ConfigureDatabase<ConfigurationDbContext>(builder.Configuration, typeof(Program));
})
// Magazyn operacyjny w bazie danych (Tokeny, Kody OIDC, Zgody użytkowników)
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b => b.ConfigureDatabase<PersistedGrantDbContext>(builder.Configuration, typeof(Program));
    options.EnableTokenCleanup = true;
    options.TokenCleanupInterval = 3600;
})
.AddDeveloperSigningCredential();

// 7. Konfiguracja kontrolerów MVC i Quorum Admin UI (RCL / NuGet)
builder.Services.AddControllersWithViews();
builder.Services.AddQuorumAdminUI<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
    options.SeedData = true;
});

var app = builder.Build();

// Przetwarzanie nagłówków Proxy przed routingiem
app.UseForwardedHeaders();

// 8. Automatyczna migracja i Seedowanie danych początkowych (w tym federacji Entra ID, Azure B2C, Google OIDC)
await SeedData.EnsureSeedDataAsync(app);

// Inicjalizacja i załadowanie dynamicznych schematów OIDC do pamięci
using (var scope = app.Services.CreateScope())
{
    var dynamicOidcService = scope.ServiceProvider.GetRequiredService<IDynamicOidcService>();
    await dynamicOidcService.ReloadFederationSchemesAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseQuorumAdminUI();

// 8. Pipeline IdentityServer i autoryzacji
app.UseIdentityServer();

app.MapDefaultControllerRoute();

app.Run();
