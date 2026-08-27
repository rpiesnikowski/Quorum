using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using System.Security.Claims;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminUserStore<TUser> : IAdminUserStore
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<IdentityRole>? _roleManager;

    public EfAdminUserStore(
        UserManager<TUser> userManager,
        RoleManager<IdentityRole>? roleManager = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<PagedResult<UserAdminModel>> GetUsersAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(s)) ||
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = new List<UserAdminModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            list.Add(new UserAdminModel
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                PhoneNumberConfirmed = u.PhoneNumberConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = u.LockoutEnd,
                AccessFailedCount = u.AccessFailedCount,
                Roles = roles.ToList()
            });
        }

        return new PagedResult<UserAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<UserAdminModel?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        return new UserAdminModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            Roles = roles.ToList(),
            Claims = claims.Select(c => new UserClaimModel { Type = c.Type, Value = c.Value }).ToList()
        };
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(UserAdminModel model, CancellationToken cancellationToken = default)
    {
        var user = new TUser
        {
            UserName = model.UserName,
            Email = model.Email,
            EmailConfirmed = model.EmailConfirmed,
            PhoneNumber = model.PhoneNumber,
            PhoneNumberConfirmed = model.PhoneNumberConfirmed,
            TwoFactorEnabled = model.TwoFactorEnabled,
            LockoutEnabled = model.LockoutEnabled
        };

        IdentityResult result;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            result = await _userManager.CreateAsync(user, model.NewPassword);
        }
        else
        {
            result = await _userManager.CreateAsync(user);
        }

        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        model.Id = user.Id;

        // Assign roles
        if (model.Roles != null && model.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, model.Roles);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(UserAdminModel model, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
            return (false, $"Użytkownik o ID '{model.Id}' nie został znaleziony.");

        user.UserName = model.UserName;
        user.Email = model.Email;
        user.EmailConfirmed = model.EmailConfirmed;
        user.PhoneNumber = model.PhoneNumber;
        user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;
        user.TwoFactorEnabled = model.TwoFactorEnabled;
        user.LockoutEnabled = model.LockoutEnabled;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // Synchronize roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var desiredRoles = model.Roles ?? new List<string>();

        var rolesToRemove = currentRoles.Except(desiredRoles).ToList();
        if (rolesToRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        var rolesToAdd = desiredRoles.Except(currentRoles).ToList();
        if (rolesToAdd.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        // Change password if provided
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!passResult.Succeeded)
            {
                return (false, "Użytkownik zaktualizowany, ale błąd hasła: " + string.Join("; ", passResult.Errors.Select(e => e.Description)));
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return (true, null);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string id, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return (false, "Nie znaleziono użytkownika.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleLockoutAsync(string id, bool lockAccount, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return (false, "Nie znaleziono użytkownika.");

        if (lockAccount)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return (true, null);
    }

    public async Task<List<string>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        if (_roleManager != null)
        {
            return await _roleManager.Roles.Select(r => r.Name!).Where(n => n != null).ToListAsync(cancellationToken);
        }

        return new List<string> { "Admin", "User", "Manager", "Developer" };
    }
}
