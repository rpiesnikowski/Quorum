using System.ComponentModel.DataAnnotations;

namespace Quorum.FineGrainedAuth.AuthZen.Models;

/// <summary>
/// Model administracyjny dla polityki autoryzacyjnej AuthZEN PAP (Policy Administration Point).
/// </summary>
public class AuthZenPolicyAdminModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa polityki jest wymagana.")]
    [StringLength(128, ErrorMessage = "Nazwa polityki nie może przekraczać 128 znaków.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(512, ErrorMessage = "Opis nie może przekraczać 512 znaków.")]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Range(-1000, 1000, ErrorMessage = "Priorytet musi mieścić się w przedziale od -1000 do 1000.")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Efekt: "Permit" (Zezwalaj) lub "Deny" (Odmów)
    /// </summary>
    [Required(ErrorMessage = "Efekt polityki jest wymagany.")]
    public string Effect { get; set; } = "Permit";

    // --- Kryteria Podmiotu (Subject) ---

    [StringLength(64)]
    public string SubjectType { get; set; } = "user";

    /// <summary>
    /// Role użytkownika (rozdzielone przecinkami, np. "Admin,Manager")
    /// </summary>
    [StringLength(512)]
    public string? SubjectRoles { get; set; }

    /// <summary>
    /// Wymagane claims użytkownika (np. {"department":"Finance"})
    /// </summary>
    [StringLength(1024)]
    public string? SubjectRequiredClaims { get; set; }

    public bool RequireActiveUser { get; set; } = true;

    public bool RequireAuthenticated { get; set; } = true;

    // --- Kryteria Akcji (Action) ---

    [Required(ErrorMessage = "Akcja jest wymagana (np. GET, POST lub *).")]
    [StringLength(128)]
    public string Action { get; set; } = "*";

    // --- Kryteria Zasobu (Resource) ---

    [StringLength(64)]
    public string ResourceType { get; set; } = "route";

    [Required(ErrorMessage = "Wzorzec zasobu jest wymagany (np. /api/orders/* lub *).")]
    [StringLength(255)]
    public string ResourcePattern { get; set; } = "*";

    [StringLength(512)]
    public string? ConditionExpression { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // --- Właściwości pomocnicze UI ---

    public bool IsPermit => Effect.Equals("Permit", StringComparison.OrdinalIgnoreCase);

    public string EffectBadgeClass => IsPermit 
        ? "bg-success text-white" 
        : "bg-danger text-white";

    public string StatusSummary => IsEnabled ? "Aktywna" : "Wyłączona";

    public string RolesSummary => !string.IsNullOrWhiteSpace(SubjectRoles) 
        ? SubjectRoles 
        : "Dowolna rola";

    public string ResourceSummary => $"{ResourceType}:{ResourcePattern}";
}
