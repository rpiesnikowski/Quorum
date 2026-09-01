using Microsoft.AspNetCore.Components;

namespace Quorum.Backend.AdminUI.Components.Layout;

public partial class AdminNavMenu : ComponentBase
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public bool IsPinned { get; set; } = false;

    [Parameter]
    public bool IsExpanded { get; set; } = false;

    [Parameter]
    public EventCallback OnPinToggle { get; set; }

    private async Task HandlePinToggle()
    {
        if (OnPinToggle.HasDelegate)
        {
            await OnPinToggle.InvokeAsync();
        }
    }

    private string GetNavLinkClass(string targetPath, bool isExact = false)
    {
        var currentRelativePath = Navigation.ToBaseRelativePath(Navigation.Uri);
        var normalizedTarget = targetPath.TrimStart('/');

        bool isActive = isExact
            ? string.Equals(currentRelativePath, normalizedTarget, StringComparison.OrdinalIgnoreCase)
            : currentRelativePath.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);

        return $"nav-link quorum-nav-link d-flex align-items-center {(isActive ? "active" : "")}";
    }
}
