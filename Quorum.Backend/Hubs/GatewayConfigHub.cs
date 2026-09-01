using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Quorum.Backend.Hubs;

/// <summary>
/// Hub SignalR odpowiedzialny za synchronizację konfiguracji reguł routingu
/// oraz przesyłanie powiadomień w czasie rzeczywistym do instancji Quorum.Backend.Gateway.
/// Obsługuje skalowanie horyzontalne wielu replik Quorum.Backend z wykorzystaniem Redis Backplane.
/// </summary>
public class GatewayConfigHub : Hub
{
    private readonly ILogger<GatewayConfigHub> _logger;

    public GatewayConfigHub(ILogger<GatewayConfigHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "GatewayClient";

        _logger.LogInformation("Instancja API Gateway połączyła się z Hubem konfiguracyjnym: ConnectionId={ConnectionId}, IP={ClientIp}, UserAgent={UserAgent}",
            Context.ConnectionId, clientIp, userAgent);

        // Dodanie do dedykowanej grupy bramek
        await Groups.AddToGroupAsync(Context.ConnectionId, "QuorumGateways");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Instancja API Gateway rozłączyła się z błędem: ConnectionId={ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Instancja API Gateway rozłączyła się normalnie: ConnectionId={ConnectionId}", Context.ConnectionId);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "QuorumGateways");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Metoda ping-pong do weryfikacji liveness / heartbeat z poziomu bramki.
    /// </summary>
    public Task<string> Ping(string instanceName)
    {
        _logger.LogDebug("Odebrano Ping od instancji Gateway: {InstanceName}", instanceName);
        return Task.FromResult($"Pong from Quorum.Backend at {DateTime.UtcNow:O}");
    }
}
