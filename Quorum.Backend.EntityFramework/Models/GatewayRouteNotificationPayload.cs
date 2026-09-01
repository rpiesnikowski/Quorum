namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Model ładunku powiadomienia integracyjnego o zmianie konfiguracji tras API Gateway.
/// Przesyłany za pośrednictwem SignalR Hub / Redis Backplane do działających instancji Quorum.Backend.Gateway.
/// </summary>
public class GatewayRouteNotificationPayload
{
    /// <summary>
    /// Identyfikator zmienionej trasy (lub null w przypadku operacji masowej / ponownego załadowania).
    /// </summary>
    public int? RouteId { get; set; }

    /// <summary>
    /// Typ wykonanej akcji: "Created", "Updated", "Deleted", "ReloadAll".
    /// </summary>
    public string Action { get; set; } = "Updated";

    /// <summary>
    /// Wzorzec ścieżki (MatchPattern) zmienionej trasy.
    /// </summary>
    public string? MatchPattern { get; set; }

    /// <summary>
    /// Data i czas wystąpienia zdarzenia w UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Opcjonalny czytelny komunikat diagnostyczny.
    /// </summary>
    public string? Message { get; set; }
}
