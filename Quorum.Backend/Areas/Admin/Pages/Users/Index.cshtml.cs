using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.Models;

namespace Quorum.Backend.Areas.Admin.Pages.Users;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public IList<UserItemViewModel> Users { get; set; } = new List<UserItemViewModel>();

    public class UserItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
    }

    public async Task OnGetAsync()
    {
        var allUsers = await _userManager.Users.AsNoTracking().ToListAsync();
        var list = new List<UserItemViewModel>();

        foreach (var user in allUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            list.Add(new UserItemViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = roles,
                CreatedAt = user.CreatedAt
            });
        }

        Users = list;
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            if (user.UserName?.ToLower() == "admin")
            {
                TempData["ErrorMessage"] = "Nie można usunąć głównego konta administratora 'admin'.";
                return RedirectToPage();
            }

            await _userManager.DeleteAsync(user);
            TempData["SuccessMessage"] = $"Użytkownik {user.UserName} został usunięty.";
        }
        return RedirectToPage();
    }
}
