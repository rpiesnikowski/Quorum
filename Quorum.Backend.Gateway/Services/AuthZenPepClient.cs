using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quorum.Backend.EntityFramework.AuthZen;

namespace Quorum.Backend.Gateway.Services;

/// <summary>
/// Domyślna implementacja klienta AuthZEN PEP komunikująca się z PDP przez protokół REST API.
/// </summary>
public class AuthZenPepClient : IAuthZenPepClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthZenPepClient> _logger;

    public AuthZenPepClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AuthZenPepClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthZenEvaluationResponse> EvaluateAsync(
        AuthZenEvaluationRequest request,
        string? customEndpoint = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Ustalenie docelowego adresu URL silnika decyzyjnego PDP
        var endpoint = !string.IsNullOrWhiteSpace(customEndpoint)
            ? customEndpoint
            : _configuration["AuthZen:PdpEndpoint"] 
              ?? _configuration["AuthZen:PdpUrl"] 
              ?? "http://localhost:5000/access/v1/evaluation";

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("[AuthZEN PEP] Weryfikacja: Podmiot='{Subject}', Akcja='{Action}', Zasób='{Resource}' -> PDP Endpoint: {Endpoint}",
                request.Subject.Id, request.Action.Name, request.Resource.Id, endpoint);

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthZenEvaluationResponse>(
                    cancellationToken: cancellationToken);

                if (result != null)
                {
                    _logger.LogInformation("[AuthZEN PEP] Decyzja PDP odebrana w {ElapsedMs}ms: Decyzja={Decision}, Powód={Reason}",
                        stopwatch.ElapsedMilliseconds, result.Decision, result.Context?.Reason ?? "Brak");
                    return result;
                }
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[AuthZEN PEP] Błąd odpowiedzi z PDP (HTTP {StatusCode}): {Body}",
                (int)response.StatusCode, errorBody);

            return new AuthZenEvaluationResponse
            {
                Decision = false,
                Context = new AuthZenEvaluationResponseContext
                {
                    Reason = $"Błąd komunikacji z silnikiem decyzyjnym PDP (HTTP {(int)response.StatusCode})",
                    Evaluator = "Quorum PEP Client (Fallback Fail-Closed)"
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AuthZEN PEP] Wyjątek podczas zapytania do PDP ({Endpoint}) po {ElapsedMs}ms",
                endpoint, stopwatch.ElapsedMilliseconds);

            // Fail-Closed: ze względów bezpieczeństwa w przypadku awarii PDP odmów dostępu
            return new AuthZenEvaluationResponse
            {
                Decision = false,
                Context = new AuthZenEvaluationResponseContext
                {
                    Reason = $"Silnik decyzyjny PDP jest niedostępny: {ex.Message}",
                    Evaluator = "Quorum PEP Client (Exception Fail-Closed)"
                }
            };
        }
    }
}
