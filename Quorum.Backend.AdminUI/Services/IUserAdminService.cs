namespace Quorum.Backend.AdminUI.Services;

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IUserAdminService
{
    Task<int> GetUsersCountAsync();
    Task<IList<AdminUserDto>> GetUsersAsync();
    Task<(bool Success, string? Error)> CreateUserAsync(string userName, string email, string password, string? fullName, string? role);
    Task<(bool Success, string? Error)> DeleteUserAsync(string id);
}
