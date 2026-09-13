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

    /// <summary>
    /// Sprawdza status dostępności i połączenia z serwerem OpenFGA REST API (0.0.0.0:8080).
    /// </summary>
    Task<FgaServerStatus> GetServerStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pobiera listę magazynów (Stores) z OpenFGA REST API (GET /stores).
    /// </summary>
    Task<IReadOnlyList<FgaStore>> ListStoresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tworzy nowy magazyn w OpenFGA (POST /stores).
    /// </summary>
    Task<FgaStore> CreateStoreAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Zapewnia istnienie aktywnego Store w OpenFGA i zwraca jego identyfikator (StoreId).
    /// </summary>
    Task<string> EnsureStoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Zwraca aktualnie skonfigurowany StoreId.
    /// </summary>
    string GetStoreId();

    /// <summary>
    /// Zmienia aktualny StoreId.
    /// </summary>
    void SetStoreId(string storeId);
}
