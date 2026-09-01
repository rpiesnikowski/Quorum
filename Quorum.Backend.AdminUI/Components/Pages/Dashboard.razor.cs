using Microsoft.AspNetCore.Components;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages;

public partial class Dashboard : ComponentBase
{
    [Inject]
    public IAdminDashboardStore DashboardStore { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private DashboardStatsModel? stats;

    protected override async Task OnInitializedAsync()
    {
        stats = await DashboardStore.GetStatsAsync();
    }
}
