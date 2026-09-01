using Quorum.Backend.EntityFramework;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.Gateway.Middleware;
using Quorum.Backend.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja usług w kontenerze DI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Rejestracja bazy danych dla kont użytkowników i dynamicznych federacji (ApplicationDbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureDatabase<ApplicationDbContext>(builder.Configuration, typeof(Program)));

// Rejestracja interfejsu IFederationDbContext dla panelu AdminUI
builder.Services.AddScoped<IFederationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IGatewayDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// 3. Rejestracja pamięci podręcznej tras w RAM (In-Memory Cache) oraz klienta SignalR do powiadomień w czasie rzeczywistym
builder.Services.AddSingleton<IGatewayRouteCache, GatewayRouteCache>();
builder.Services.AddHostedService<GatewaySignalRClientService>();

builder.Services.AddHttpClient("GatewayProxyClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        // Ignorowanie błędów certyfikatów SSL (tylko dla środowisk Dev/Test)
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

var app = builder.Build();

// Konfiguracja Swaggera w środowisku deweloperskim
if (app.Environment.IsDevelopment())
{
  
}

app.UseHttpsRedirection();
app.UseMiddleware<Proxy2ManyHostsMiddleware>();

// ---------------------------------------------

app.UseAuthorization();

app.MapControllers();

// Przykładowy endpoint typu Health Check z diagnostyką pamięci podręcznej reguł
app.MapGet("/health", (IGatewayRouteCache routeCache) => Results.Ok(new 
{ 
    Status = "Healthy", 
    Service = "Quorum.Backend.Gateway",
    CachedRoutesCount = routeCache.RouteCount,
    LastRefreshedUtc = routeCache.LastRefreshedUtc
}));

app.Run();