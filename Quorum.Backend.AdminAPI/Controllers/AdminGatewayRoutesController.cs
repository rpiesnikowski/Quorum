using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/gateway")]
[Produces("application/json")]
public class AdminGatewayRoutesController : ControllerBase
{
    private readonly IAdminGatewayStore _gatewayStore;

    public AdminGatewayRoutesController(IAdminGatewayStore gatewayStore)
    {
        _gatewayStore = gatewayStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę skonfigurowanych tras Reverse Proxy API Gateway.
    /// </summary>
    [HttpGet("routes")]
    [ProducesResponseType(typeof(PagedResult<GatewayRouteAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GatewayRouteAdminModel>>> GetRoutes(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _gatewayStore.GetRoutesAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły trasy Gateway na podstawie ID.
    /// </summary>
    [HttpGet("routes/{id:int}")]
    [ProducesResponseType(typeof(GatewayRouteAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GatewayRouteAdminModel>> GetRouteById(int id, CancellationToken cancellationToken)
    {
        var route = await _gatewayStore.GetRouteByIdAsync(id, cancellationToken);
        if (route == null)
        {
            return NotFound(new { error = $"Nie znaleziono trasy Gateway o ID {id}." });
        }

        return Ok(route);
    }

    /// <summary>
    /// Tworzy nową trasę przekierowania API Gateway (obsługuje szablony {parametry} i grupy Regex).
    /// </summary>
    [HttpPost("routes")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GatewayRouteAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateRoute([FromBody] GatewayRouteAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _gatewayStore.CreateRouteAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć trasy Gateway." });
        }

        return CreatedAtAction(nameof(GetRouteById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje istniejącą trasę Gateway.
    /// </summary>
    [HttpPut("routes/{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateRoute(int id, [FromBody] GatewayRouteAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _gatewayStore.UpdateRouteAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować trasy Gateway." });
        }

        return Ok(new { message = $"Trasa Gateway o ID {id} została pomyślnie zaktualizowana." });
    }

    /// <summary>
    /// Usuwa trasę Gateway o podanym ID.
    /// </summary>
    [HttpDelete("routes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteRoute(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _gatewayStore.DeleteRouteAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć trasy Gateway." });
        }

        return NoContent();
    }

    /// <summary>
    /// Symulator i Tester tras Gateway - wykonuje ewaluację dopasowania, wyodrębnia grupy parametrów, weryfikuje scope/uprawnienia i symuluje żądanie proxy.
    /// </summary>
    [HttpPost("test")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GatewayTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GatewayTestResult>> TestRoute([FromBody] GatewayTestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "Ścieżka wejściowa (Path) jest wymagana." });
        }

        var result = await _gatewayStore.TestRouteAsync(request, cancellationToken);
        return Ok(result);
    }
}
