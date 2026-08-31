using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/clients")]
[Produces("application/json")]
public class AdminClientsController : ControllerBase
{
    private readonly IAdminClientStore _clientStore;

    public AdminClientsController(IAdminClientStore clientStore)
    {
        _clientStore = clientStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę klientów OAuth2/OIDC z opcjonalnym wyszukiwaniem.
    /// </summary>
    /// <param name="search">Fraza wyszukiwania (ClientId lub ClientName)</param>
    /// <param name="page">Numer strony (domyślnie 1)</param>
    /// <param name="pageSize">Rozmiar strony (domyślnie 10)</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientAdminModel>>> GetClients(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _clientStore.GetClientsAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegółowe dane klienta OAuth2/OIDC na podstawie identyfikatora ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAdminModel>> GetClientById(int id, CancellationToken cancellationToken)
    {
        var client = await _clientStore.GetClientByIdAsync(id, cancellationToken);
        if (client == null)
        {
            return NotFound(new { error = $"Nie znaleziono klienta o ID {id}." });
        }

        return Ok(client);
    }

    /// <summary>
    /// Tworzy nowego klienta OAuth2/OIDC.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ClientAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateClient([FromBody] ClientAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _clientStore.CreateClientAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć klienta." });
        }

        return CreatedAtAction(nameof(GetClientById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje istniejącego klienta OAuth2/OIDC.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateClient(int id, [FromBody] ClientAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _clientStore.UpdateClientAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować klienta." });
        }

        return Ok(new { message = $"Klient o ID {id} został pomyślnie zaktualizowany." });
    }

    /// <summary>
    /// Usuwa klienta OAuth2/OIDC o podanym identyfikatorze ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteClient(int id, CancellationToken cancellationToken)
    {
        var (success, error) = await _clientStore.DeleteClientAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć klienta." });
        }

        return NoContent();
    }

    /// <summary>
    /// Dodaje nowy sekret (hasło klienta) do wskazanego klienta.
    /// </summary>
    [HttpPost("{id:int}/secrets")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddSecret(int id, [FromBody] ClientSecretModel secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret.Value))
        {
            return BadRequest(new { error = "Wartość sekretu (Value) jest wymagana." });
        }

        var (success, error) = await _clientStore.AddSecretAsync(id, secret, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się dodać sekretu." });
        }

        return Ok(new { message = "Sekret został pomyślnie dodany do klienta." });
    }

    /// <summary>
    /// Usuwa sekret klienta o podanym ID sekretu.
    /// </summary>
    [HttpDelete("{id:int}/secrets/{secretId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteSecret(int id, int secretId, CancellationToken cancellationToken)
    {
        var (success, error) = await _clientStore.DeleteSecretAsync(id, secretId, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć sekretu klienta." });
        }

        return NoContent();
    }
}
