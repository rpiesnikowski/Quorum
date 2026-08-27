using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.AdminUI2.Extensions;
using Quorum.Backend.EntityFramework;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend2.Components;
using Quorum.Backend2.Data;
using Quorum.Backend2.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

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

// 8. Konfiguracja Quorum AdminUI 2 (Nuget RCL oparty w 100% o Radzen)
builder.Services.AddQuorumAdminUI2<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
});

// Rejestracja abstrakcyjnej implementacji Entity Framework Core dla magazynów CRUD
builder.Services.AddQuorumAdminUI2EntityFrameworkStore<ApplicationUser>();

var app = builder.Build();

app.UseStaticFiles();
app.MapStaticAssets();
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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// 10. Pipeline IdentityServer
app.UseIdentityServer();

// 11. Endpointy logowania, wylogowania i federacji OIDC
app.MapPost("/account/login", async (
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    [FromForm] string userName,
    [FromForm] string password,
    [FromForm] bool? rememberMe,
    [FromForm] string? returnUrl) =>
{
    var user = await userManager.FindByNameAsync(userName) ?? await userManager.FindByEmailAsync(userName);
    if (user != null)
    {
        var result = await signInManager.PasswordSignInAsync(user.UserName!, password, rememberMe ?? false, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/") ? returnUrl : "/admin";
            return Results.LocalRedirect(target);
        }
    }

    var encodedError = Uri.EscapeDataString("Nieprawidłowa nazwa użytkownika lub hasło.");
    var redirectUrl = $"/account/login?error={encodedError}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/admin")}";
    return Results.LocalRedirect(redirectUrl);
}).DisableAntiforgery();

app.MapGet("/account/logout", async (
    SignInManager<ApplicationUser> signInManager,
    [FromQuery] string? returnUrl) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect(returnUrl ?? "/");
});

app.MapPost("/account/logout", async (
    SignInManager<ApplicationUser> signInManager,
    [FromForm] string? returnUrl) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect(returnUrl ?? "/");
}).DisableAntiforgery();

app.MapGet("/Account/ExternalLogin", (
    SignInManager<ApplicationUser> signInManager,
    [FromQuery] string provider,
    [FromQuery] string? returnUrl) =>
{
    var redirectUrl = $"/Account/ExternalLoginCallback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/admin")}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, [provider]);
});

app.MapGet("/Account/ExternalLoginCallback", async (
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    [FromQuery] string? returnUrl,
    [FromQuery] string? remoteError) =>
{
    if (remoteError != null)
    {
        return Results.LocalRedirect($"/account/login?error={Uri.EscapeDataString($"Błąd zewnętrznego dostawcy: {remoteError}")}");
    }

    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        return Results.LocalRedirect("/account/login?error=Nie+udalo+sie+pobrac+danych+logowania+zewnetrznego");
    }

    var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
    if (signInResult.Succeeded)
    {
        return Results.LocalRedirect(returnUrl ?? "/admin");
    }

    var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;

    if (!string.IsNullOrEmpty(email))
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser == null)
        {
            existingUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(existingUser);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(existingUser, "User");
            }
        }

        if (existingUser != null)
        {
            await userManager.AddLoginAsync(existingUser, info);
            await signInManager.SignInAsync(existingUser, isPersistent: true);
            return Results.LocalRedirect(returnUrl ?? "/admin");
        }
    }

    return Results.LocalRedirect("/account/login?error=Nie+udalo+sie+zalogowac+przez+SSO");
});

// 12. Mapowanie komponentów Blazor (z automatycznym wykrywaniem stron i komponentów z Quorum.Backend.AdminUI2)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Quorum.Backend.AdminUI2.Components.Layout.AdminLayout).Assembly
    );

app.Run();
