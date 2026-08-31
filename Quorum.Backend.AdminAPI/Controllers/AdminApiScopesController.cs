using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/scopes")]
[Produces("application/json")]
public class AdminApiScopesController : ControllerBase
{
    private readonly IAdminApiScopeStore _scopeStore;

    public AdminApiScopesController(IAdminApiScopeStore scopeStore)
    {
        _scopeStore = scopeStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę zakresów API (Scopes).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ApiScopeAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ApiScopeAdminModel>>> GetScopes(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _scopeStore.GetScopesAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły zakresu API na podstawie ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiScopeAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiScopeAdminModel>> GetScopeById(int id, CancellationToken cancellationToken)
    {
        var scope = await _scopeStore.GetScopeByIdAsync(id, cancellationToken);
        if (scope == null)
        {
            return NotFound(new { error = $"Nie znaleziono zakresu API o ID {id}." });
        }

        return Ok(scope);
    }

    /// <summary>
    /// Tworzy nowy zakres API (Scope).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiScopeAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateScope([FromBody] ApiScopeAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _scopeStore.CreateScopeAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć zakresu API." });
        }

        return CreatedAtAction(nameof(GetScopeById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje istniejący zakres API.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateScope(int id, [FromBody] ApiScopeAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _scopeStore.UpdateScopeAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować zakresu API." });
        }

        return Ok(new { message = $"Zakres API o ID {id} został pomyślnie zaktualizowany." });
    }

    /// <summary>
    /// Usuwa zakres API o podanym ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteScope(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _scopeStore.DeleteScopeAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć zakresu API." });
        }

        return NoContent();
    }
}
