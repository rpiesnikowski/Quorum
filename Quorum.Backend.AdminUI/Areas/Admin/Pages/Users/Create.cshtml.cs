using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Services;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Users;

public class CreateModel : PageModel
{
    private readonly IUserAdminService _userService;

    public CreateModel(IUserAdminService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<string> AvailableRoles { get; set; } = new List<string>();

    public class InputModel
    {
        [Required(ErrorMessage = "Login / nazwa konta jest wymagana.")]
        [Display(Name = "Login / Nazwa Użytkownika")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adres email jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu email.")]
        [Display(Name = "Adres Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Imię i Nazwisko")]
        public string? FullName { get; set; }

        [Phone(ErrorMessage = "Nieprawidłowy numer telefonu.")]
        [Display(Name = "Numer Telefonu")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Hasło musi mieć co najmniej {2} znaków.")]
        [DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Potwierdź adres email")]
        public bool EmailConfirmed { get; set; } = true;

        [Display(Name = "Przypisane Role")]
        public List<string> SelectedRoles { get; set; } = new() { "User" };
    }

    public async Task OnGetAsync()
    {
        AvailableRoles = await _userService.GetAvailableRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AvailableRoles = await _userService.GetAvailableRolesAsync();

        if (!ModelState.IsValid) return Page();

        var (success, error, createdUserId) = await _userService.CreateUserAsync(
            Input.UserName,
            Input.Email,
            Input.Password,
            Input.FullName,
            Input.PhoneNumber,
            Input.EmailConfirmed,
            Input.SelectedRoles);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Wystąpił błąd podczas tworzenia użytkownika.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Konto użytkownika '{Input.UserName}' zostało pomyślnie utworzone.";
        return RedirectToPage("Edit", new { id = createdUserId });
    }
}
