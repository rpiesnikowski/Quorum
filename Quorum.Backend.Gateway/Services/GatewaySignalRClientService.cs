using Microsoft.AspNetCore.SignalR.Client;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.Gateway.Services;

/// <summary>
/// Serwis tła (BackgroundService) łączący bramkę Quorum.Backend.Gateway z Hubem SignalR w Quorum.Backend.
/// Nasłuchuje zdarzeń OnGatewayRoutesUpdated / OnRoutesChanged i natychmiast odświeża pamięć podręczną reguł proxy.
/// </summary>
public class GatewaySignalRClientService : BackgroundService
{
    private readonly IGatewayRouteCache _routeCache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GatewaySignalRClientService> _logger;
    private HubConnection? _hubConnection;

    public GatewaySignalRClientService(
        IGatewayRouteCache routeCache,
        IConfiguration configuration,
        ILogger<GatewaySignalRClientService> logger)
    {
        _routeCache = routeCache;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Inicjalne załadowanie reguł do pamięci podręcznej przy starcie bramki
        try
        {
            _logger.LogInformation("[SignalR Client] Inicjalizacja pamięci podręcznej tras API Gateway...");
            await _routeCache.RefreshAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR Client] Nie udało się wykonać wstępnego ładowania reguł przy starcie.");
        }

        var isEnabled = _configuration.GetValue<bool>("GatewayHub:Enabled", true);
        if (!isEnabled)
        {
            _logger.LogInformation("[SignalR Client] Klient SignalR dla API Gateway został wyłączony w konfiguracji (GatewayHub:Enabled=false).");
            return;
        }

        var hubUrl = _configuration["GatewayHub:Url"] 
            ?? _configuration["GatewayNotification:HubUrl"]
            ?? "https://localhost:5001/hubs/gateway-config";

        _logger.LogInformation("[SignalR Client] Konfiguracja połączenia z Hubem: {HubUrl}", hubUrl);

        // 2. Budowa połączenia HubConnection z polityką wznawiania połączenia
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Ignorowanie certyfikatów deweloperskich (tylko w Dev)
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // 3. Rejestracja obsługi zdarzeń aktualizacji tras z Quorum.Backend
        _hubConnection.On<GatewayRouteNotificationPayload>("OnGatewayRoutesUpdated", async payload =>
        {
            await HandleRouteUpdateAsync(payload, stoppingToken);
        });

        _hubConnection.On<GatewayRouteNotificationPayload>("OnRoutesChanged", async payload =>
        {
            await HandleRouteUpdateAsync(payload, stoppingToken);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "[SignalR Client] Utracono połączenie z Hubem Quorum.Backend. Próba ponownego nawiązania...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("[SignalR Client] Pomyślnie wznowiono połączenie z Hubem. Nowe ConnectionId: {ConnectionId}. Odświeżanie tras...", connectionId);
            return _routeCache.RefreshAsync(stoppingToken);
        };

        _hubConnection.Closed += async error =>
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(error, "[SignalR Client] Połączenie zostało trwale zamknięte. Restart pętli nasłuchującej za 5 sekund...");
                await Task.Delay(5000, stoppingToken);
                await TryConnectWithRetryAsync(stoppingToken);
            }
        };

        // 4. Rozpoczęcie nasłuchiwania w tle z pętlą retry
        await TryConnectWithRetryAsync(stoppingToken);

        // Utrzymywanie działania w tle dopóki usługa nie zostanie zatrzymana
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Prawidłowe zatrzymanie usługi
        }
    }

    private async Task HandleRouteUpdateAsync(GatewayRouteNotificationPayload payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🔔 [SignalR Client] ODEBRANO POWIADOMIENIE O ZMIANIE REGUL ROUTINGU: Akcja={Action}, RouteId={RouteId}, Wzorzec={MatchPattern}, Wiadomość='{Message}'",
            payload.Action, payload.RouteId, payload.MatchPattern, payload.Message);

        try
        {
            await _routeCache.RefreshAsync(cancellationToken);
            _logger.LogInformation("✅ [SignalR Client] Pamięć podręczna tras API Gateway została natychmiast zaktualizowana. Liczba aktywnych tras: {Count}", _routeCache.RouteCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [SignalR Client] Błąd podczas automatycznego odświeżania tras po odebraniu powiadomienia.");
        }
    }

    private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        if (_hubConnection == null) return;

        while (!stoppingToken.IsCancellationRequested && _hubConnection.State == HubConnectionState.Disconnected)
        {
            try
            {
                _logger.LogInformation("[SignalR Client] Nawiązywanie połączenia z Hubem SignalR w Quorum.Backend...");
                await _hubConnection.StartAsync(stoppingToken);
                _logger.LogInformation("🚀 [SignalR Client] Połączono z Hubem powiadomień API Gateway! Stan połączenia: {State}", _hubConnection.State);
                
                // Po udanym połączeniu odświeżamy reguły, aby upewnić się, że nie przegapiliśmy żadnych zmian w trakcie rozłączenia
                await _routeCache.RefreshAsync(stoppingToken);
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("[SignalR Client] Nie udało się połączyć z Hubem ({Message}). Ponowna próba za 5 sekund...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync(cancellationToken);
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SignalR Client] Błąd podczas zamykania połączenia SignalR.");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
