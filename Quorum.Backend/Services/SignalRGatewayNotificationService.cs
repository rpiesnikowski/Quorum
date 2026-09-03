using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Models;
using Quorum.Backend.Hubs;

namespace Quorum.Backend.Services;

/// <summary>
/// Implementacja IGatewayNotificationService oparta o ASP.NET Core SignalR oraz opcjonalny Redis Backplane.
/// Emituje zdarzenia OnGatewayRoutesUpdated / OnRoutesChanged do wszystkich połączonych instancji bramki Quorum.Backend.Gateway.
/// </summary>
public class SignalRGatewayNotificationService : IGatewayNotificationService
{
    private readonly IHubContext<GatewayConfigHub> _hubContext;
    private readonly ILogger<SignalRGatewayNotificationService> _logger;

    public SignalRGatewayNotificationService(
        IHubContext<GatewayConfigHub> hubContext,
        ILogger<SignalRGatewayNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyRoutesChangedAsync(GatewayRouteNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Rozgłaszanie powiadomienia o aktualizacji Gateway Routing przez SignalR/Redis: Akcja={Action}, RouteId={RouteId}, Wzorzec={MatchPattern}",
                payload.Action, payload.RouteId, payload.MatchPattern);

            // Wysyłamy zdarzenie RPC 'OnGatewayRoutesUpdated' do wszystkich klientów w grupie lub ogólnie
            await _hubContext.Clients.All.SendAsync("OnGatewayRoutesUpdated", payload, cancellationToken);
            
            // Kompatybilny alias dla wstecznej zgodności
            await _hubContext.Clients.All.SendAsync("OnRoutesChanged", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas rozgłaszania sygnału powiadomienia do instancji Gateway przez SignalR.");
            throw;
        }
    }
}
