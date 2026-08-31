using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Produces("application/json")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardStore _dashboardStore;

    public AdminDashboardController(IAdminDashboardStore dashboardStore)
    {
        _dashboardStore = dashboardStore;
    }

    /// <summary>
    /// Pobiera zagregowane statystyki i podsumowanie dashboardu administratora.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DashboardStatsModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardStatsModel>> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardStore.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }
}
