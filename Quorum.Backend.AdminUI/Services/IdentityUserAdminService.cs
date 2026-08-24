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

    public async Task<int> GetUsersCountAsync()
    {
        return await _userManager.Users.CountAsync();
    }

    public async Task<IList<string>> GetAvailableRolesAsync()
    {
        var defaultRoles = new List<string> { "Admin", "User", "Manager" };
        if (_roleManager != null)
        {
            var dbRoles = await _roleManager.Roles.Select(r => r.Name!).Where(n => !string.IsNullOrEmpty(n)).ToListAsync();
            foreach (var r in dbRoles)
            {
                if (!defaultRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                {
                    defaultRoles.Add(r);
                }
            }
        }
        return defaultRoles;
    }

    public async Task<IList<AdminUserDto>> GetUsersAsync(string? search = null)
    {
        IQueryable<TUser> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(s)) ||
                (u.Email != null && u.Email.Contains(s)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var users = await query.ToListAsync();
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
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                FullName = fullName,
                Roles = roles,
                TwoFactorEnabled = user.TwoFactorEnabled,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                AccessFailedCount = user.AccessFailedCount,
                CreatedAt = createdAt
            });
        }

        return result;
    }

    public async Task<AdminUserDto?> GetUserByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var fullNameProp = typeof(TUser).GetProperty("FullName", BindingFlags.Public | BindingFlags.Instance);
        var createdAtProp = typeof(TUser).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance);

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

        return new AdminUserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            FullName = fullName,
            Roles = roles,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            CreatedAt = createdAt
        };
    }

    public async Task<(bool Success, string? Error, string? CreatedUserId)> CreateUserAsync(
        string userName,
        string email,
        string password,
        string? fullName,
        string? phoneNumber,
        bool emailConfirmed,
        IList<string> roles)
    {
        var user = new TUser
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            EmailConfirmed = emailConfirmed,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim()
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
            return (false, error, null);
        }

        if (roles.Count > 0)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    if (_roleManager != null && !await _roleManager.RoleExistsAsync(role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }
                    await _userManager.AddToRoleAsync(user, role);
                }
            }
        }

        return (true, null, user.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(
        string id,
        string userName,
        string email,
        string? fullName,
        string? phoneNumber,
        bool emailConfirmed,
        bool twoFactorEnabled,
        bool lockoutEnabled,
        IList<string> roles)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, "Użytkownik nie istnieje.");
        }

        user.UserName = userName.Trim();
        user.Email = email.Trim();
        user.EmailConfirmed = emailConfirmed;
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        user.TwoFactorEnabled = twoFactorEnabled;
        user.LockoutEnabled = lockoutEnabled;

        var fullNameProp = typeof(TUser).GetProperty("FullName", BindingFlags.Public | BindingFlags.Instance);
        if (fullNameProp != null && fullNameProp.CanWrite)
        {
            fullNameProp.SetValue(user, string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim());
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var error = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return (false, error);
        }

        // Aktualizacja ról
        var currentRoles = await _userManager.GetRolesAsync(user);
        var targetRoles = roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var rolesToAdd = targetRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToRemove = currentRoles.Except(targetRoles, StringComparer.OrdinalIgnoreCase).ToList();

        // Blokada odebrania roli Admin głównemu kontu admin
        if (string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase) && rolesToRemove.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            rolesToRemove.Remove("Admin");
            if (!targetRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            {
                targetRoles.Add("Admin");
            }
        }

        if (rolesToAdd.Count > 0)
        {
            foreach (var role in rolesToAdd)
            {
                if (_roleManager != null && !await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        if (rolesToRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return (false, "Hasło musi mieć co najmniej 6 znaków.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, "Użytkownik nie istnieje.");
        }

        // Usunięcie starego hasła i ustawienie nowego
        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (hasPassword)
        {
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                var error = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return (false, error);
            }
        }

        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            var error = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return (false, error);
        }

        // Odświeżenie SecurityStamp, aby wylogować potencjalne nieautoryzowane sesje
        await _userManager.UpdateSecurityStampAsync(user);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UnlockUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, "Użytkownik nie istnieje.");
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, error);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
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

    public async Task<(bool Succeeded, AdminUserDto? User, string? ErrorMessage)> ValidateAdminCredentialsAsync(string userNameOrEmail, string password, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return (false, null, "Wprowadź login/email oraz hasło.");
        }

        var user = await _userManager.FindByNameAsync(userNameOrEmail)
                ?? await _userManager.FindByEmailAsync(userNameOrEmail);

        if (user == null)
        {
            return (false, null, "Nieprawidłowy login lub hasło administratora.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            return (false, null, "Nieprawidłowy login lub hasło administratora.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!string.IsNullOrEmpty(requiredRole) && !roles.Contains(requiredRole, StringComparer.OrdinalIgnoreCase))
        {
            return (false, null, $"Konto '{user.UserName}' nie posiada uprawnień administratora (wymagana rola: {requiredRole}).");
        }

        var fullNameProp = typeof(TUser).GetProperty("FullName", BindingFlags.Public | BindingFlags.Instance);
        string? fullName = fullNameProp?.GetValue(user) as string;

        var dto = new AdminUserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? user.Id,
            Email = user.Email ?? string.Empty,
            FullName = fullName,
            Roles = roles
        };

        return (true, dto, null);
    }
}
