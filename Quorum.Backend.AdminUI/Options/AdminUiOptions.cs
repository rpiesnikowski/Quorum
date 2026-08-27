namespace Quorum.Backend.AdminUI.Options;

public class AdminUiOptions
{
    /// <summary>
    /// Nazwa schematu uwierzytelniania dla administratorów (domyślnie "Quorum.Admin.Auth").
    /// </summary>
    public string AuthenticationScheme { get; set; } = "Quorum.Admin.Auth";

    /// <summary>
    /// Nazwa ciasteczka sesyjnego administratora (domyślnie "Quorum.Admin.Auth").
    /// </summary>
    public string CookieName { get; set; } = "Quorum.Admin.Auth";

    /// <summary>
    /// Ścieżka logowania dedykowana dla administratorów.
    /// </summary>
    public string LoginPath { get; set; } = "/Admin/Account/Login";

    /// <summary>
    /// Ścieżka wylogowania dedykowana dla administratorów.
    /// </summary>
    public string LogoutPath { get; set; } = "/Admin/Account/Logout";

    /// <summary>
    /// Ścieżka strony odmowy dostępu dla panelu administratora.
    /// </summary>
    public string AccessDeniedPath { get; set; } = "/Admin/Account/AccessDenied";

    /// <summary>
    /// Czas ważności ciasteczka sesyjnego administratora.
    /// </summary>
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Nazwa wymaganej roli do wejścia do panelu administracyjnego (domyślnie "Admin").
    /// </summary>
    public string RequiredRole { get; set; } = "Admin";

    /// <summary>
    /// Nazwa polityki autoryzacyjnej (domyślnie "RequireAdminRole").
    /// </summary>
    public string PolicyName { get; set; } = "RequireAdminRole";

    /// <summary>
    /// Ścieżka bazowa dla obszaru administracyjnego (domyślnie "/").
    /// </summary>
    public string AreaFolder { get; set; } = "/";

    /// <summary>
    /// Czy włączyć automatyczne zabezpieczenie autoryzacją obszaru /Admin.
    /// </summary>
    public bool EnableAuthorization { get; set; } = true;

    /// <summary>
    /// Czy zasilić bazę danymi początkowymi.
    /// </summary>
    public bool SeedData { get; set; }
}

public class AdminUiOptions2 : AdminUiOptions
{
}
