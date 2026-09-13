using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Services.PIP;

/// <summary>
/// PIP (Policy Information Point): Punkt informacyjny o polityce w architekturze AuthZEN / ABAC.
/// Źródło atrybutów: pobiera dodatkowe informacje niezbędne do podjęcia decyzji
/// (dane użytkownika z AspNetCoreIdentity, role, status konta, claims, dział w firmie).
/// </summary>
public interface IAuthZenPolicyInformationPoint
{
    /// <summary>
    /// Pobiera kompletny zestaw atrybutów podmiotu (Subject) na podstawie ID, loginu lub emaila z bazy Identity.
    /// </summary>
    Task<AuthZenSubjectAttributes?> GetSubjectAttributesAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wzbogaca obiekt AuthZenSubject o pobrane z bazy Identity atrybuty i claimsy.
    /// </summary>
    Task EnrichSubjectAsync(AuthZenSubject subject, CancellationToken cancellationToken = default);
}
