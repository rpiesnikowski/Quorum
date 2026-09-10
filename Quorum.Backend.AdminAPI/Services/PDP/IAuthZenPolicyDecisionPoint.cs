using Quorum.Backend.EntityFramework.AuthZen;

namespace Quorum.Backend.AdminAPI.Services.PDP;

/// <summary>
/// PDP (Policy Decision Point): Silnik decyzyjny w architekturze AuthZEN / ABAC.
/// Odbiera zapytanie ewaluacyjne od PEP, ocenia je w oparciu o wczytane reguły/polityki autoryzacyjne
/// oraz atrybuty pobrane z PIP i zwraca ostateczną decyzję (true/false) wraz z uzasadnieniem.
/// </summary>
public interface IAuthZenPolicyDecisionPoint
{
    /// <summary>
    /// Ewaluuje pojedyncze zapytanie autoryzacyjne (POST /access/v1/evaluation).
    /// </summary>
    Task<AuthZenEvaluationResponse> EvaluateAsync(
        AuthZenEvaluationRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ewaluuje wsadowe zapytanie autoryzacyjne (POST /access/v1/evaluations).
    /// </summary>
    Task<AuthZenBatchEvaluationResponse> EvaluateBatchAsync(
        AuthZenBatchEvaluationRequest request, 
        CancellationToken cancellationToken = default);
}
