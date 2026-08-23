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

    public class InputModel
    {
        [Required(ErrorMessage = "Login jest wymagany")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email jest wymagany")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu email")]
        public string Email { get; set; } = string.Empty;

        public string? FullName { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Hasło musi mieć co najmniej {2} znaków.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "User";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (success, error) = await _userService.CreateUserAsync(
            Input.UserName,
            Input.Email,
            Input.Password,
            Input.FullName,
            Input.Role);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Wystąpił błąd podczas tworzenia użytkownika.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Konto użytkownika '{Input.UserName}' zostało utworzone.";
        return RedirectToPage("Index");
    }
}
