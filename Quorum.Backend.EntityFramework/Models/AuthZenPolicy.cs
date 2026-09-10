using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Reprezentuje regułę / politykę autoryzacyjną AuthZEN zarządzaną w PAP (Policy Administration Point).
/// Oceniana przez PDP (Policy Decision Point) w oparciu o atrybuty pobrane z PIP (AspNetCore Identity / Claims).
/// </summary>
[Table("AuthZenPolicies")]
public class AuthZenPolicy
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unikalna, czytelna nazwa polityki (np. "AdminsFullAccess", "BlockLockedAccounts", "FinanceOrdersRead")
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Opis biznesowy i uzasadnienie polityki bezpieczeństwa
    /// </summary>
    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>
    /// Czy polityka jest aktywna i brana pod uwagę przez silnik decyzyjny PDP
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Priorytet ewaluacji (wyższy priorytet = sprawdzany wcześniej w silniku decyzyjnym PDP)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Efekt decyzji w przypadku dopasowania reguły: "Permit" (zezwolenie) lub "Deny" (odmowa / blokada)
    /// </summary>
    [Required]
    [MaxLength(16)]
    public string Effect { get; set; } = "Permit";

    // --- Kryteria Podmiotu (Subject Criteria) ---

    /// <summary>
    /// Oczekiwany typ podmiotu (np. "user", "service", "*" dla dowolnego)
    /// </summary>
    [MaxLength(64)]
    public string SubjectType { get; set; } = "user";

    /// <summary>
    /// Wymagane role użytkownika z ASP.NET Core Identity (rozdzielone przecinkami, np. "Admin,Manager")
    /// Użytkownik musi posiadać przynajmniej jedną z wymienionych ról (OR).
    /// Puste oznacza brak wymogu konkretnej roli.
    /// </summary>
    [MaxLength(512)]
    public string? SubjectRoles { get; set; }

    /// <summary>
    /// Wymagane claims użytkownika w formacie JSON lub par klucz=wartość (np. {"department":"Finance","tier":"Gold"})
    /// </summary>
    [MaxLength(1024)]
    public string? SubjectRequiredClaims { get; set; }

    /// <summary>
    /// Czy użytkownik musi być zweryfikowany i aktywny w Identity (niezablokowany LockoutEnd, IsActive == true)
    /// </summary>
    public bool RequireActiveUser { get; set; } = true;

    /// <summary>
    /// Czy wymagany jest uwierzytelniony użytkownik (zalogowany z tokenem/tożsamością)
    /// </summary>
    public bool RequireAuthenticated { get; set; } = true;

    // --- Kryteria Akcji (Action Criteria) ---

    /// <summary>
    /// Akcja lub metoda HTTP (np. "GET", "POST", "GET,POST", "*", "read", "write")
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Action { get; set; } = "*";

    // --- Kryteria Zasobu (Resource Criteria) ---

    /// <summary>
    /// Typ zasobu (np. "route", "api", "document", "*" dla dowolnego)
    /// </summary>
    [MaxLength(64)]
    public string ResourceType { get; set; } = "route";

    /// <summary>
    /// Wzorzec zasobu (ścieżka, wzorzec z wieloznacznikami '*' lub wyrażenie regularne Regex, np. "^/api/orders.*" lub "/api/v1/*")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ResourcePattern { get; set; } = "*";

    /// <summary>
    /// Opcjonalne dodatkowe wyrażenie warunkowe (np. reguła IP, godziny dostępu, atrybut środowiska)
    /// </summary>
    [MaxLength(512)]
    public string? ConditionExpression { get; set; }

    /// <summary>
    /// Data utworzenia polityki w PAP
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data ostatniej modyfikacji
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
