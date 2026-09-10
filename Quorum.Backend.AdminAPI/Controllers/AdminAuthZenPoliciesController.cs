using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

/// <summary>
/// Kontroler REST API dla panelu PAP (Policy Administration Point) w standardzie AuthZEN.
/// Umożliwia zarządzanie regułami autoryzacyjnymi przez zewnętrzne systemy i panel administracyjny.
/// </summary>
[ApiController]
[Route("api/admin/authzen/policies")]
[Produces("application/json")]
public class AdminAuthZenPoliciesController : ControllerBase
{
    private readonly IAdminAuthZenPolicyStore _policyStore;

    public AdminAuthZenPoliciesController(IAdminAuthZenPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    /// <summary>
    /// Pobiera listę polityk autoryzacyjnych z możliwością filtrowania i stronicowania.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuthZenPolicyAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuthZenPolicyAdminModel>>> GetPolicies(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _policyStore.GetPoliciesAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły pojedynczej polityki autoryzacyjnej po ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AuthZenPolicyAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthZenPolicyAdminModel>> GetPolicyById(int id, CancellationToken cancellationToken)
    {
        var policy = await _policyStore.GetPolicyByIdAsync(id, cancellationToken);
        if (policy == null)
        {
            return NotFound(new { error = $"Nie znaleziono polityki AuthZEN o ID {id}." });
        }

        return Ok(policy);
    }

    /// <summary>
    /// Tworzy nową politykę autoryzacyjną w bazie PAP.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthZenPolicyAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthZenPolicyAdminModel>> CreatePolicy(
        [FromBody] AuthZenPolicyAdminModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _policyStore.CreatePolicyAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Błąd podczas tworzenia polityki autoryzacyjnej." });
        }

        return CreatedAtAction(nameof(GetPolicyById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje istniejącą politykę autoryzacyjną.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePolicy(
        int id,
        [FromBody] AuthZenPolicyAdminModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest(new { error = "Niezgodność identyfikatorów trasy i modelu." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _policyStore.UpdatePolicyAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Błąd podczas aktualizacji polityki." });
        }

        return Ok(model);
    }

    /// <summary>
    /// Usuwa politykę autoryzacyjną po ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePolicy(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _policyStore.DeletePolicyAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Błąd podczas usuwania polityki." });
        }

        return NoContent();
    }

    /// <summary>
    /// Przełącza status aktywności (IsEnabled) polityki.
    /// </summary>
    [HttpPost("{id:int}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TogglePolicy(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _policyStore.TogglePolicyStatusAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Błąd podczas zmiany statusu polityki." });
        }

        return Ok(new { success = true, id });
    }
}
