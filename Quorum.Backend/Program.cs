using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI.Extensions;
using Quorum.Backend.Data;
using Quorum.Backend.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Obsługa nagłówków X-Forwarded-Proto / X-Forwarded-For dla Reverse Proxy (Docker/Nginx/Caddy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 2. Rejestracja bazy danych dla kont użytkowników (ApplicationDbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureDatabase<ApplicationDbContext>(builder.Configuration));

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Działa bez problemu zarówno na https://localhost:5001 jak i http://localhost:5000
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

// 5. Konfiguracja Open.IdentityServer (RSK) z Entity Framework
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
    options.ConfigureDbContext = b => b.ConfigureDatabase<ConfigurationDbContext>(builder.Configuration);
})
// Magazyn operacyjny w bazie danych (Tokeny, Kody OIDC, Zgody użytkowników)
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b => b.ConfigureDatabase<PersistedGrantDbContext>(builder.Configuration);
    options.EnableTokenCleanup = true;
    options.TokenCleanupInterval = 3600;
})
.AddDeveloperSigningCredential();

// 6. Konfiguracja kontrolerów MVC i Quorum Admin UI (RCL / NuGet)
builder.Services.AddControllersWithViews();
builder.Services.AddQuorumAdminUI<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
});

var app = builder.Build();

// Przetwarzanie nagłówków Proxy przed routingiem
app.UseForwardedHeaders();

// 7. Automatyczna migracja i Seedowanie danych początkowych
await SeedData.EnsureSeedDataAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseQuorumAdminUI();

app.UseRouting();

// 8. Pipeline IdentityServer i autoryzacji
app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
