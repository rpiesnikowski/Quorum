using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/grants")]
[Produces("application/json")]
public class AdminGrantsController : ControllerBase
{
    private readonly IAdminGrantStore _grantStore;

    public AdminGrantsController(IAdminGrantStore grantStore)
    {
        _grantStore = grantStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę aktywnych grantów, tokenów odświeżających (Refresh Tokens), kodów autoryzacji i zgód.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PersistedGrantAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PersistedGrantAdminModel>>> GetGrants(
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? clientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _grantStore.GetGrantsAsync(search, type, clientId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły pojedynczego grantu na podstawie unikalnego klucza (Key).
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(PersistedGrantAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersistedGrantAdminModel>> GetGrantByKey(string key, CancellationToken cancellationToken)
    {
        var grant = await _grantStore.GetGrantByKeyAsync(key, cancellationToken);
        if (grant == null)
        {
            return NotFound(new { error = $"Nie znaleziono grantu o kluczu {key}." });
        }

        return Ok(grant);
    }

    /// <summary>
    /// Unieważnia (usuwa) pojedynczy grant / token na podstawie klucza.
    /// </summary>
    [HttpDelete("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RevokeGrant(string key, CancellationToken cancellationToken)
    {
        var (success, error) = await _grantStore.RevokeGrantAsync(key, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się unieważnić grantu." });
        }

        return NoContent();
    }

    /// <summary>
    /// Unieważnia wszystkie aktywne sesje i granty dla wskazanego użytkownika (SubjectId).
    /// </summary>
    [HttpDelete("subject/{subjectId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RevokeAllForSubject(string subjectId, CancellationToken cancellationToken)
    {
        var (success, error) = await _grantStore.RevokeAllForSubjectAsync(subjectId, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się unieważnić grantów użytkownika." });
        }

        return NoContent();
    }

    /// <summary>
    /// Unieważnia wszystkie aktywne tokeny wydane dla wskazanego klienta (ClientId).
    /// </summary>
    [HttpDelete("client/{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RevokeAllForClient(string clientId, CancellationToken cancellationToken)
    {
        var (success, error) = await _grantStore.RevokeAllForClientAsync(clientId, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się unieważnić grantów klienta." });
        }

        return NoContent();
    }
}
