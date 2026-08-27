using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Models;

public class ApiScopeAdminModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa zakresu (Name) jest wymagana.")]
    [StringLength(200, ErrorMessage = "Nazwa zakresu nie może przekraczać 200 znaków.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Nazwa wyświetlana nie może przekraczać 200 znaków.")]
    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public bool Required { get; set; } = false;

    public bool Emphasize { get; set; } = false;

    public bool ShowInDiscoveryDocument { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public List<string> UserClaims { get; set; } = new();

    public string UserClaimsSummary => UserClaims != null && UserClaims.Count > 0 ? string.Join(", ", UserClaims) : "Brak";
    public string StatusSummary => Enabled ? "Aktywny" : "Wyłączony";

    public string UserClaimsText
    {
        get => string.Join(Environment.NewLine, UserClaims);
        set => UserClaims = (value ?? "")
            .Split(new[] { '\r', '\n', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
    }
}
