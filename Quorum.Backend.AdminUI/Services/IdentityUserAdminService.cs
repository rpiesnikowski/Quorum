using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Quorum.Backend.AdminUI.Services;

public class IdentityUserAdminService<TUser> : IUserAdminService where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<IdentityRole>? _roleManager;

    public IdentityUserAdminService(
        UserManager<TUser> userManager,
        RoleManager<IdentityRole>? roleManager = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IList<AdminUserDto>> GetUsersAsync()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var result = new List<AdminUserDto>();

        var fullNameProp = typeof(TUser).GetProperty("FullName", BindingFlags.Public | BindingFlags.Instance);
        var createdAtProp = typeof(TUser).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            string? fullName = null;
            if (fullNameProp != null)
            {
                fullName = fullNameProp.GetValue(user) as string;
            }

            DateTime createdAt = DateTime.UtcNow;
            if (createdAtProp != null)
            {
                var val = createdAtProp.GetValue(user);
                if (val is DateTime dt)
                {
                    createdAt = dt;
                }
            }

            result.Add(new AdminUserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = fullName,
                Roles = roles,
                CreatedAt = createdAt
            });
        }

        return result;
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(string userName, string email, string password, string? fullName, string? role)
    {
        var user = new TUser
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true
        };

        var fullNameProp = typeof(TUser).GetProperty("FullName", BindingFlags.Public | BindingFlags.Instance);
        if (fullNameProp != null && fullNameProp.CanWrite && !string.IsNullOrWhiteSpace(fullName))
        {
            fullNameProp.SetValue(user, fullName.Trim());
        }

        var createdAtProp = typeof(TUser).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance);
        if (createdAtProp != null && createdAtProp.CanWrite)
        {
            createdAtProp.SetValue(user, DateTime.UtcNow);
        }

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, error);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (_roleManager != null)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
            await _userManager.AddToRoleAsync(user, role);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, "Użytkownik nie istnieje.");
        }

        if (string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Nie można usunąć głównego konta administratora 'admin'.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, error);
        }

        return (true, null);
    }
}
