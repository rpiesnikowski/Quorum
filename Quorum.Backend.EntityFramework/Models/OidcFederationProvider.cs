using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Model reprezentujący konfigurację dynamicznego dostawcy tożsamości OpenID Connect (OIDC).
/// Umożliwia rejestrację, edycję i usuwanie zewnętrznych federacji w czasie rzeczywistym bez restartu aplikacji.
/// </summary>
public class OidcFederationProvider
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Unikalna nazwa schematu autentykacji (np. "entra-id", "azure-b2c", "google-oidc").
    /// </summary>
    [Required(ErrorMessage = "Identyfikator schematu (Scheme) jest wymagany")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Schemat może zawierać tylko litery, cyfry, myślniki i podkreślenia")]
    [Display(Name = "Nazwa schematu (Scheme)")]
    public string Scheme { get; set; } = string.Empty;

    /// <summary>
    /// Czytelna nazwa wyświetlana na przycisku logowania (np. "Microsoft Entra ID", "Azure AD B2C", "Google Workspace").
    /// </summary>
    [Required(ErrorMessage = "Nazwa wyświetlana jest wymagana")]
    [Display(Name = "Nazwa wyświetlana")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Główny adres serwera tożsamości OIDC (Issuer/Authority URL, np. "https://login.microsoftonline.com/{tenant}/v2.0").
    /// </summary>
    [Required(ErrorMessage = "Adres Authority URL jest wymagany")]
    [Url(ErrorMessage = "Wprowadź prawidłowy adres URL (np. https://...)")]
    [Display(Name = "Authority (OIDC Issuer URL)")]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Identyfikator klienta (Client ID / Application ID) zarejestrowany w zewnętrznym providerze OIDC.
    /// </summary>
    [Required(ErrorMessage = "Client ID jest wymagany")]
    [Display(Name = "Client ID (Application ID)")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Klucz tajny klienta (Client Secret) - opcjonalny przy użyciu Public Client / PKCE.
    /// </summary>
    [Display(Name = "Client Secret")]
    [DataType(DataType.Password)]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Typ odpowiedzi OIDC (domyślnie "code" dla Authorization Code Flow).
    /// </summary>
    [Required]
    [Display(Name = "Response Type")]
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// Żądane zakresy OIDC rozdzielone spacją (domyślnie "openid profile email").
    /// </summary>
    [Required]
    [Display(Name = "Scopes")]
    public string Scope { get; set; } = "openid profile email";

    /// <summary>
    /// Ścieżka zwrotna przekierowania po zalogowaniu (domyślnie np. "/signin-oidc-entra").
    /// </summary>
    [Required]
    [Display(Name = "Callback Path")]
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Ścieżka zwrotna po wylogowaniu (domyślnie np. "/signout-callback-oidc").
    /// </summary>
    [Display(Name = "Signed Out Callback Path")]
    public string? SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>
    /// Wymuszenie Proof Key for Code Exchange (PKCE) dla podwyższonego bezpieczeństwa.
    /// </summary>
    [Display(Name = "Włącz PKCE (Proof Key for Code Exchange)")]
    public bool UsePkce { get; set; } = true;

    /// <summary>
    /// Czy pobierać dodatkowe oświadczenia użytkownika z punktu końcowego UserInfo.
    /// </summary>
    [Display(Name = "Pobieraj Claims z UserInfo Endpoint")]
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Czy zapisywać otrzymane tokeny (access_token, id_token, refresh_token) w kontekście sesji.
    /// </summary>
    [Display(Name = "Zapisuj tokeny w sesji (SaveTokens)")]
    public bool SaveTokens { get; set; } = true;

    /// <summary>
    /// Czy dana federacja jest aktywna i dostępna na ekranie logowania.
    /// </summary>
    [Display(Name = "Aktywna federacja")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Automatyczne tworzenie konta w bazie AspNetIdentity po pierwszym pomyślnym logowaniu OIDC.
    /// </summary>
    [Display(Name = "Auto-provisioning użytkowników w Identity")]
    public bool AutoProvisionUsers { get; set; } = true;

    /// <summary>
    /// Domyślna rola przypisywana nowo tworzonym użytkownikom z tej federacji (np. "User", "Manager").
    /// </summary>
    [Display(Name = "Domyślna rola Identity")]
    public string DefaultRole { get; set; } = "User";

    /// <summary>
    /// Typ ikony wyświetlanej na przycisku: "microsoft", "azure", "google", "openid", "generic".
    /// </summary>
    [Display(Name = "Ikona dostawcy")]
    public string IconType { get; set; } = "openid";

    /// <summary>
    /// Kolor tła lub akcentu przycisku logowania (np. "#0078D4", "#4285F4").
    /// </summary>
    [Display(Name = "Kolor przycisku (HEX)")]
    public string? ButtonColor { get; set; }

    /// <summary>
    /// Parametr monitu OIDC (np. "select_account", "consent", "login").
    /// </summary>
    [Display(Name = "Parametr Prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    /// Dodatkowe parametry przekazywane do Authorization Endpoint w formacie JSON (np. {"p": "b2c_1_susi"}).
    /// </summary>
    [Display(Name = "Dodatkowe parametry URL (JSON)")]
    public string? AdditionalParametersJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
