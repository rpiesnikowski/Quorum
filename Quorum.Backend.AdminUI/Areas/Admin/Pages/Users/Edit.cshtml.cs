using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Services;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Users;

public class EditModel : PageModel
{
    private readonly IUserAdminService _userService;

    public EditModel(IUserAdminService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public UserEditInputModel Input { get; set; } = new();

    [BindProperty]
    public PasswordResetInputModel PasswordInput { get; set; } = new();

    public AdminUserDto? CurrentUser { get; set; }
    public IList<string> AvailableRoles { get; set; } = new List<string>();

    public class UserEditInputModel
    {
        [Required(ErrorMessage = "Identyfikator użytkownika jest wymagany.")]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwa użytkownika (login) jest wymagana.")]
        [Display(Name = "Nazwa Użytkownika / Login")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adres email jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu email.")]
        [Display(Name = "Adres Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Email Potwierdzony")]
        public bool EmailConfirmed { get; set; } = true;

        [Display(Name = "Imię i Nazwisko")]
        public string? FullName { get; set; }

        [Phone(ErrorMessage = "Nieprawidłowy numer telefonu.")]
        [Display(Name = "Numer Telefonu")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Uwierzytelnianie Dwuskładnikowe (2FA)")]
        public bool TwoFactorEnabled { get; set; }

        [Display(Name = "Blokada Konta Włączona (Lockout Enabled)")]
        public bool LockoutEnabled { get; set; } = true;

        [Display(Name = "Przypisane Role")]
        public List<string> SelectedRoles { get; set; } = new();
    }

    public class PasswordResetInputModel
    {
        [Required(ErrorMessage = "Wprowadź nowe hasło.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Nowe hasło musi mieć co najmniej {2} znaków.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nowe Hasło")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Potwierdź Nowe Hasło")]
        [Compare("NewPassword", ErrorMessage = "Wprowadzone hasła nie są identyczne.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage("Index");
        }

        CurrentUser = await _userService.GetUserByIdAsync(id);
        if (CurrentUser == null)
        {
            TempData["ErrorMessage"] = "Użytkownik o podanym identyfikatorze nie został odnaleziony.";
            return RedirectToPage("Index");
        }

        AvailableRoles = await _userService.GetAvailableRolesAsync();

        Input = new UserEditInputModel
        {
            Id = CurrentUser.Id,
            UserName = CurrentUser.UserName,
            Email = CurrentUser.Email,
            EmailConfirmed = CurrentUser.EmailConfirmed,
            FullName = CurrentUser.FullName,
            PhoneNumber = CurrentUser.PhoneNumber,
            TwoFactorEnabled = CurrentUser.TwoFactorEnabled,
            LockoutEnabled = CurrentUser.LockoutEnabled,
            SelectedRoles = CurrentUser.Roles.ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(string id)
    {
        AvailableRoles = await _userService.GetAvailableRolesAsync();
        CurrentUser = await _userService.GetUserByIdAsync(id);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (success, error) = await _userService.UpdateUserAsync(
            id,
            Input.UserName,
            Input.Email,
            Input.FullName,
            Input.PhoneNumber,
            Input.EmailConfirmed,
            Input.TwoFactorEnabled,
            Input.LockoutEnabled,
            Input.SelectedRoles);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Wystąpił błąd podczas aktualizacji danych użytkownika.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Dane użytkownika '{Input.UserName}' zostały pomyślnie zaktualizowane.";
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        AvailableRoles = await _userService.GetAvailableRolesAsync();
        CurrentUser = await _userService.GetUserByIdAsync(id);

        if (string.IsNullOrWhiteSpace(PasswordInput.NewPassword) || PasswordInput.NewPassword.Length < 6)
        {
            ModelState.AddModelError("PasswordInput.NewPassword", "Hasło musi mieć co najmniej 6 znaków.");
            return Page();
        }

        if (PasswordInput.NewPassword != PasswordInput.ConfirmPassword)
        {
            ModelState.AddModelError("PasswordInput.ConfirmPassword", "Hasła nie są identyczne.");
            return Page();
        }

        var (success, error) = await _userService.ResetPasswordAsync(id, PasswordInput.NewPassword);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Wystąpił błąd podczas zmiany hasła.");
            return Page();
        }

        TempData["SuccessMessage"] = "Hasło użytkownika zostało pomyślnie zmienione.";
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostUnlockAsync(string id)
    {
        var (success, error) = await _userService.UnlockUserAsync(id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Wystąpił błąd podczas odblokowywania konta.";
        }
        else
        {
            TempData["SuccessMessage"] = "Konto użytkownika zostało pomyślnie odblokowane.";
        }

        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (success, error) = await _userService.DeleteUserAsync(id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Wystąpił błąd podczas usuwania użytkownika.";
            return RedirectToPage("Edit", new { id });
        }

        TempData["SuccessMessage"] = "Użytkownik został pomyślnie usunięty.";
        return RedirectToPage("Index");
    }
}
