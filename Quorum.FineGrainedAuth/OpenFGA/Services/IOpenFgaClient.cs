using Quorum.FineGrainedAuth.OpenFGA.Models;

namespace Quorum.FineGrainedAuth.OpenFGA.Services;

/// <summary>
/// Klient integracyjny dla silnika OpenFGA (Fine-Grained Authorization wg modelu Google Zanzibar).
/// Zapewnia operacje sprawdzania relacji (Check), odczytu (Read), zapisu (Write) oraz analizy grafu uprawnień (Expand).
/// </summary>
public interface IOpenFgaClient
{
    /// <summary>
    /// Sprawdza, czy dany użytkownik/podmiot posiada wskazaną relację do obiektu (np. user:john jest viewer dokumentu doc:1).
    /// </summary>
    Task<FgaCheckResponse> CheckAsync(FgaCheckRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Zapisuje lub usuwa relacje (Tuples) w magazynie OpenFGA.
    /// </summary>
    Task<bool> WriteTuplesAsync(IEnumerable<FgaTupleKey> writes, IEnumerable<FgaTupleKey>? deletes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Odczytuje relacje z magazynu OpenFGA na podstawie filtrów.
    /// </summary>
    Task<IReadOnlyList<FgaTupleKey>> ReadTuplesAsync(string? user = null, string? relation = null, string? obj = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rozwija strukturę relacji dla zadanego obiektu i relacji (drzewo decyzyjne).
    /// </summary>
    Task<FgaExpandResponse> ExpandAsync(string relation, string obj, CancellationToken cancellationToken = default);
}
