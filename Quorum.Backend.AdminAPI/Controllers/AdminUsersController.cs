using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserStore _userStore;

    public AdminUsersController(IAdminUserStore userStore)
    {
        _userStore = userStore;
    }

    /// <summary>
    /// Pobiera stronicowaną listę użytkowników ASP.NET Identity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserAdminModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserAdminModel>>> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _userStore.GetUsersAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera szczegóły użytkownika na podstawie ID (GUID/String).
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserAdminModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAdminModel>> GetUserById(string id, CancellationToken cancellationToken)
    {
        var user = await _userStore.GetUserByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = $"Nie znaleziono użytkownika o ID {id}." });
        }

        return Ok(user);
    }

    /// <summary>
    /// Tworzy nowego użytkownika z hasłem i przypisanymi rolami.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(UserAdminModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateUser([FromBody] UserAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error) = await _userStore.CreateUserAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się utworzyć użytkownika." });
        }

        return CreatedAtAction(nameof(GetUserById), new { id = model.Id }, model);
    }

    /// <summary>
    /// Aktualizuje profil użytkownika, adres email, status blokady i role.
    /// </summary>
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateUser(string id, [FromBody] UserAdminModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        model.Id = id;
        var (success, error) = await _userStore.UpdateUserAsync(model, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zaktualizować użytkownika." });
        }

        return Ok(new { message = $"Użytkownik {id} został pomyślnie zaktualizowany." });
    }

    /// <summary>
    /// Usuwa konto użytkownika.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        var (success, error) = await _userStore.DeleteUserAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się usunąć użytkownika." });
        }

        return NoContent();
    }

    /// <summary>
    /// Zmienia / resetuje hasło użytkownika.
    /// </summary>
    [HttpPost("{id}/change-password")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(string id, [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error = "Nowe hasło (NewPassword) jest wymagane." });
        }

        var (success, error) = await _userStore.ChangePasswordAsync(id, request.NewPassword, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zmienić hasła użytkownika." });
        }

        return Ok(new { message = "Hasło użytkownika zostało pomyślnie zmienione." });
    }

    /// <summary>
    /// Blokuje lub odblokowuje konto użytkownika.
    /// </summary>
    [HttpPost("{id}/toggle-lockout")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ToggleLockout(string id, [FromBody] ToggleLockoutRequest request, CancellationToken cancellationToken)
    {
        var (success, error) = await _userStore.ToggleLockoutAsync(id, request.LockAccount, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Nie udało się zmienić statusu blokady konta." });
        }

        return Ok(new { message = $"Status blokady konta został pomyślnie zmieniony na: {(request.LockAccount ? "Zablokowane" : "Aktywne")}." });
    }

    /// <summary>
    /// Pobiera listę wszystkich zdefiniowanych ról w systemie (np. Admin, User, Manager).
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetAllRoles(CancellationToken cancellationToken)
    {
        var roles = await _userStore.GetAllRolesAsync(cancellationToken);
        return Ok(roles);
    }
}

public class ChangePasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class ToggleLockoutRequest
{
    public bool LockAccount { get; set; }
}
