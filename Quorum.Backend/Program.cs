using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Quorum.Backend.Data;
using Quorum.Backend.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Rejestracja bazy danych dla kont użytkowników (ApplicationDbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureDatabase<ApplicationDbContext>(builder.Configuration));

// 2. Konfiguracja ASP.NET Core Identity
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

// 3. Konfiguracja Open.IdentityServer (RSK) z Entity Framework
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

// 4. Konfiguracja kontrolerów MVC i Razor Pages (Admin Panel CRUD)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(options =>
{
    // Zabezpieczenie katalogu /Admin rolą Administratora
    options.Conventions.AuthorizeAreaFolder("Admin", "/", "RequireAdministratorRole");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Admin"));
});

// Konfiguracja ciasteczek logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// 5. Automatyczna migracja i Seedowanie danych początkowych
await SeedData.EnsureSeedDataAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 6. Pipeline IdentityServer i autoryzacji
app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
