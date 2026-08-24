using Quorum.Backend.EntityFramework;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.Gateway.Middleware;

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

// Przykładowy endpoint typu Health Check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Quorum.Backend.Gateway" }));

app.Run();