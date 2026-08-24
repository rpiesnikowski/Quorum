namespace Quorum.Backend.AdminUI.Services;

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string? FullName { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
    public int AccessFailedCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IUserAdminService
{
    Task<int> GetUsersCountAsync();
    Task<IList<AdminUserDto>> GetUsersAsync(string? search = null);
    Task<AdminUserDto?> GetUserByIdAsync(string id);
    Task<IList<string>> GetAvailableRolesAsync();
    Task<(bool Success, string? Error, string? CreatedUserId)> CreateUserAsync(
        string userName,
        string email,
        string password,
        string? fullName,
        string? phoneNumber,
        bool emailConfirmed,
        IList<string> roles);
    Task<(bool Success, string? Error)> UpdateUserAsync(
        string id,
        string userName,
        string email,
        string? fullName,
        string? phoneNumber,
        bool emailConfirmed,
        bool twoFactorEnabled,
        bool lockoutEnabled,
        IList<string> roles);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string id, string newPassword);
    Task<(bool Success, string? Error)> UnlockUserAsync(string id);
    Task<(bool Success, string? Error)> DeleteUserAsync(string id);
    Task<(bool Succeeded, AdminUserDto? User, string? ErrorMessage)> ValidateAdminCredentialsAsync(string userNameOrEmail, string password, string requiredRole);
}
