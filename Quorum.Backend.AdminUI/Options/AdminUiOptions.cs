namespace Quorum.Backend.AdminUI.Options;

/// <summary>
/// Opcje konfiguracyjne panelu administracyjnego Quorum Admin UI.
/// </summary>
public class AdminUiOptions
{
    /// <summary>
    /// Nazwa wymaganej roli do wejścia do panelu administracyjnego (domyślnie "Admin").
    /// </summary>
    public string RequiredRole { get; set; } = "Admin";

    /// <summary>
    /// Nazwa polityki autoryzacyjnej (domyślnie "RequireAdministratorRole").
    /// </summary>
    public string PolicyName { get; set; } = "RequireAdministratorRole";

    /// <summary>
    /// Ścieżka bazowa dla obszaru administracyjnego (domyślnie "Admin").
    /// </summary>
    public string AreaFolder { get; set; } = "/";

    /// <summary>
    /// Czy włączyć automatyczne zabezpieczenie autoryzacją obszaru /Admin.
    /// </summary>
    public bool EnableAuthorization { get; set; } = true;
}
