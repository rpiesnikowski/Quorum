using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.Backend.AdminUI.Services;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Users;

public class IndexModel : PageModel
{
    private readonly IUserAdminService _userService;

    public IndexModel(IUserAdminService userService)
    {
        _userService = userService;
    }

    public IList<AdminUserDto> Users { get; set; } = new List<AdminUserDto>();

    public async Task OnGetAsync()
    {
        Users = await _userService.GetUsersAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (success, error) = await _userService.DeleteUserAsync(id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Wystąpił błąd podczas usuwania użytkownika.";
        }
        else
        {
            TempData["SuccessMessage"] = "Użytkownik został pomyślnie usunięty.";
        }
        return RedirectToPage();
    }
}
