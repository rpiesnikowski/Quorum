using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Services.PEP;

/// <summary>
/// Klient egzekwowania polityk PEP (Policy Enforcement Point) w standardzie AuthZEN.
/// Odpowiada za wysyłanie zapytań ewaluacyjnych do silnika decyzyjnego PDP.
/// </summary>
public interface IAuthZenPepClient
{
    /// <summary>
    /// Wysyła zapytanie do PDP z pytaniem: „Czy ten podmiot może wykonać tę akcję na tym zasobie?”.
    /// </summary>
    Task<AuthZenEvaluationResponse> EvaluateAsync(
        AuthZenEvaluationRequest request, 
        string? customEndpoint = null, 
        CancellationToken cancellationToken = default);
}
