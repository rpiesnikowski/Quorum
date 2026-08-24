namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// DTO reprezentujące zakres (Scope) do wyboru w interfejsie użytkownika
/// </summary>
public class ScopeItemDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "API"; // "Identity" lub "API"
    public bool Emphasize { get; set; }
}
