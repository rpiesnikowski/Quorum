namespace Quorum.Backend.AdminUI2.Models;

public class DashboardStatsModel
{
    public int ClientsCount { get; set; }
    public int ApiScopesCount { get; set; }
    public int IdentityResourcesCount { get; set; }
    public int UsersCount { get; set; }
    public int FederationsCount { get; set; }
    public int ActiveFederationsCount { get; set; }
    public int GatewayRoutesCount { get; set; }
    public int ActiveGrantsCount { get; set; }

    public List<RecentActivityModel> RecentActivities { get; set; } = new();
}

public class RecentActivityModel
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Icon { get; set; } = "info";
    public string BadgeVariant { get; set; } = "primary";
}
