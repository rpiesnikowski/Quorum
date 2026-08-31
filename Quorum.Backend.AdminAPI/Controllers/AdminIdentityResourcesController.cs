using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/identity-resources")]
[Produces("application/json")]
public class AdminIdentityResourcesController : ControllerBase
{
    private readonly IAdminIdentityResourceStore _resourceStore;

    public AdminIdentityResourcesController(IAdminIdentityResourceStore resourceStore)
    {
        _resourceStore = resourceStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę zasobów tożsamości OIDC (Identity Resources).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<IdentityResourceAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IdentityResourceAdminModel>>> GetResources(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _resourceStore.GetResourcesAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły zasobu tożsamości na podstawie ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(IdentityResourceAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IdentityResourceAdminModel>> GetResourceById(int id, CancellationToken cancellationToken)
    {
        var resource = await _resourceStore.GetResourceByIdAsync(id, cancellationToken);
        if (resource == null)
        {
            return NotFound(new { error = $"Nie znaleziono zasobu tożsamości o ID {id}." });
        }

        return Ok(resource);
    }

    /// <summary>
    /// Tworzy nowy zasób tożsamości (np. custom claim resource).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(IdentityResourceAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateResource([FromBody] IdentityResourceAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _resourceStore.CreateResourceAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć zasobu tożsamości." });
        }

        return CreatedAtAction(nameof(GetResourceById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje istniejący zasób tożsamości.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateResource(int id, [FromBody] IdentityResourceAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _resourceStore.UpdateResourceAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować zasobu tożsamości." });
        }

        return Ok(new { message = $"Zasób tożsamości o ID {id} został pomyślnie zaktualizowany." });
    }

    /// <summary>
    /// Usuwa zasób tożsamości o podanym ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteResource(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _resourceStore.DeleteResourceAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć zasobu tożsamości." });
        }

        return NoContent();
    }

    /// <summary>
    /// Inicjalizuje standardowe zasoby tożsamości OpenID Connect (openid, profile, email, address, phone).
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SeedStandardResources(CancellationToken cancellationToken)
    {
        var (success, error) = await _resourceStore.SeedStandardResourcesAsync(cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zainicjalizować standardowych zasobów." });
        }

        return Ok(new { message = "Standardowe zasoby tożsamości OIDC zostały pomyślnie zainicjalizowane." });
    }
}
