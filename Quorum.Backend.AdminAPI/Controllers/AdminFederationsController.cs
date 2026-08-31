using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/federations")]
[Produces("application/json")]
public class AdminFederationsController : ControllerBase
{
    private readonly IAdminFederationStore _federationStore;

    public AdminFederationsController(IAdminFederationStore federationStore)
    {
        _federationStore = federationStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę dostawców federacji OIDC (Single Sign-On).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FederationAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FederationAdminModel>>> GetProviders(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _federationStore.GetProvidersAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły dostawcy federacji na podstawie ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FederationAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FederationAdminModel>> GetProviderById(int id, CancellationToken cancellationToken)
    {
        var provider = await _federationStore.GetProviderByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound(new { error = $"Nie znaleziono dostawcy federacji o ID {id}." });
        }

        return Ok(provider);
    }

    /// <summary>
    /// Rejestruje nowego dostawcę federacji OIDC (np. Google, Azure AD, Keycloak).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FederationAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateProvider([FromBody] FederationAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _federationStore.CreateProviderAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć dostawcy federacji." });
        }

        return CreatedAtAction(nameof(GetProviderById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje konfigurację dostawcy federacji OIDC.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateProvider(int id, [FromBody] FederationAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _federationStore.UpdateProviderAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować dostawcy federacji." });
        }

        return Ok(new { message = $"Dostawca federacji o ID {id} został pomyślnie zaktualizowany." });
    }

    /// <summary>
    /// Usuwa dostawcę federacji OIDC.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteProvider(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _federationStore.DeleteProviderAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć dostawcy federacji." });
        }

        return NoContent();
    }

    /// <summary>
    /// Włącza lub wyłącza dostawcę federacji OIDC.
    /// </summary>
    [HttpPost("{id:int}/toggle")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ToggleStatus(int id, [FromBody] ToggleProviderStatusRequest request, CancellationToken cancellationToken)
    {
        var (success, error) = await _federationStore.ToggleStatusAsync(id, request.IsEnabled, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zmienić statusu dostawcy." });
        }

        return Ok(new { message = $"Status dostawcy ID {id} został zmieniony na: {(request.IsEnabled ? "Włączony" : "Wyłączony")}." });
    }

    /// <summary>
    /// Waliduje i testuje endpoint OIDC Discovery (.well-known/openid-configuration) dla podanego Authority URL.
    /// </summary>
    [HttpPost("test-discovery")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(DiscoveryValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscoveryValidationResult>> TestDiscovery([FromBody] TestDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Authority))
        {
            return BadRequest(new { error = "Adres Authority jest wymagany." });
        }

        var result = await _federationStore.TestDiscoveryAsync(request.Authority, cancellationToken);
        return Ok(result);
    }
}

public class ToggleProviderStatusRequest
{
    public bool IsEnabled { get; set; }
}

public class TestDiscoveryRequest
{
    public string Authority { get; set; } = string.Empty;
}
