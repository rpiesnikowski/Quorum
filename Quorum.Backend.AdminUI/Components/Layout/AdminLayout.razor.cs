using Microsoft.AspNetCore.Components;

namespace Quorum.Backend.AdminUI.Components.Layout;

public partial class AdminLayout : LayoutComponentBase
{
    private bool isPinned = false;
    private bool isHovered = false;
    private bool isExpanded => isPinned || isHovered;

    private void HandleMouseEnter()
    {
        if (!isPinned)
        {
            isHovered = true;
            StateHasChanged();
        }
    }

    private void HandleMouseLeave()
    {
        if (!isPinned)
        {
            isHovered = false;
            StateHasChanged();
        }
    }

    private void ToggleSidebarPin()
    {
        isPinned = !isPinned;
        if (!isPinned)
        {
            // Po odpięciu, jeśli kursor jest poza sidebarem, zwijamy natychmiast
            isHovered = false;
        }
        StateHasChanged();
    }

    private string GetLayoutStyle()
    {
        var width = isExpanded ? "260px" : "68px";
        return $"--sidebar-active-width: {width}; grid-template-columns: {width} 1fr;";
    }
}
