namespace Quorum.Backend.AdminUI2.Options;

public class AdminUi2Options
{
    public string BasePath { get; set; } = "/admin";
    public string Title { get; set; } = "Quorum Identity Admin UI 2";
    public string RequiredRole { get; set; } = "Admin";
    public bool EnableAuthorization { get; set; } = true;
    public string AuthenticationScheme { get; set; } = "QuorumAdmin2Cookie";
    public string CookieName { get; set; } = "Quorum.AdminUI2.Auth";
    public string LoginPath { get; set; } = "/Account/Login";
    public string LogoutPath { get; set; } = "/Account/Logout";
    public string AccessDeniedPath { get; set; } = "/Account/AccessDenied";
}
