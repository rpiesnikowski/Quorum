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

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public int TotalCount { get; set; }
    public int AdminCount { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _userService.GetUsersAsync(Search);
        TotalCount = Users.Count;
        AdminCount = Users.Count(u => u.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase));
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
        return RedirectToPage(new { search = Search });
    }
}
