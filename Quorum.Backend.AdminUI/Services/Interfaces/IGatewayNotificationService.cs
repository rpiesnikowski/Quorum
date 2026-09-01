using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Services.Interfaces;

/// <summary>
/// Interfejs usługi powiadamiania o zmianach w konfiguracji tras API Gateway w czasie rzeczywistym.
/// Pozwala na integrację z SignalR, Redis Backplane, Webhookami lub kolejkami komunikatów.
/// </summary>
public interface IGatewayNotificationService
{
    /// <summary>
    /// Wysyła powiadomienie o utworzeniu, aktualizacji, usunięciu lub przeładowaniu tras do instancji Quorum.Backend.Gateway.
    /// </summary>
    Task NotifyRoutesChangedAsync(GatewayRouteNotificationPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pusta implementacja (Fallback/No-op) używana, gdy integracja powiadomień w czasie rzeczywistym nie jest skonfigurowana.
/// </summary>
public class NullGatewayNotificationService : IGatewayNotificationService
{
    public Task NotifyRoutesChangedAsync(GatewayRouteNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
