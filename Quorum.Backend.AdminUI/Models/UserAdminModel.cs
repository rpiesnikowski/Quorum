using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Models;

public class UserAdminModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa użytkownika jest wymagana.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nazwa użytkownika musi mieć od 3 do 100 znaków.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    public string Email { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; } = true;

    [Phone(ErrorMessage = "Podaj poprawny numer telefonu.")]
    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public bool LockoutEnabled { get; set; } = true;

    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;

    public List<string> Roles { get; set; } = new();

    public string RolesSummary => Roles != null && Roles.Count > 0 ? string.Join(", ", Roles) : "Brak";
    public string StatusSummary => IsLockedOut ? "Zablokowany" : "Aktywny";

    public List<UserClaimModel> Claims { get; set; } = new();

    // Pole pomocnicze do ustawiania nowego hasła podczas tworzenia lub resetu
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Hasło musi mieć co najmniej 6 znaków.")]
    public string? NewPassword { get; set; }

    public string? ConfirmPassword { get; set; }
}
