using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quorum.FineGrainedAuth.OpenFGA.Models;
using Quorum.FineGrainedAuth.OpenFGA.Services;

namespace Quorum.FineGrainedAuth.OpenFGA.Controllers;

/// <summary>
/// Kontroler REST API do zarządzania regułami autoryzacji zdefiniowanymi w standardzie AuthZEN,
/// które są automatycznie mapowane i zapisywane przez adapter do API REST OpenFGA (0.0.0.0:8080).
/// </summary>
[ApiController]
[Route("api/openfga")]
[Produces("application/json")]
public class OpenFgaRulesController : ControllerBase
{
    private readonly IAuthZenOpenFgaStore _store;
    private readonly IOpenFgaClient _fgaClient;

    public OpenFgaRulesController(IAuthZenOpenFgaStore store, IOpenFgaClient fgaClient)
    {
        _store = store;
        _fgaClient = fgaClient;
    }

    /// <summary>
    /// Pobiera listę wszystkich reguł zdefiniowanych w formacie AuthZEN wraz z ich odwzorowaniem na krotki OpenFGA.
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(IReadOnlyList<AuthZenRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthZenRuleDto>>> GetRules(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var rules = await _store.GetRulesAsync(search, cancellationToken);
        return Ok(rules);
    }

    /// <summary>
    /// Pobiera szczegóły pojedynczej reguły po identyfikatorze ID.
    /// </summary>
    [HttpGet("rules/{id}")]
    [ProducesResponseType(typeof(AuthZenRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthZenRuleDto>> GetRuleById(string id, CancellationToken cancellationToken)
    {
        var rule = await _store.GetRuleByIdAsync(id, cancellationToken);
        if (rule == null)
        {
            return NotFound(new { error = $"Nie znaleziono reguły o ID '{id}'." });
        }

        return Ok(rule);
    }

    /// <summary>
    /// Tworzy nową regułę w obiekcie AuthZEN i natychmiast zapisuje ją przez adapter do API REST OpenFGA (POST /stores/{id}/write).
    /// </summary>
    [HttpPost("rules")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthZenRuleSyncResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthZenRuleSyncResult>> CreateRule(
        [FromBody] AuthZenRuleDto rule,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rule.SubjectId) || string.IsNullOrWhiteSpace(rule.Action) || string.IsNullOrWhiteSpace(rule.ResourceId))
        {
            return BadRequest(new { error = "Pola SubjectId, Action oraz ResourceId są wymagane w specyfikacji AuthZEN." });
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            rule.Name = $"{rule.SubjectType}:{rule.SubjectId} -> {rule.Action} -> {rule.ResourceType}:{rule.ResourceId}";
        }

        var result = await _store.CreateRuleAsync(rule, cancellationToken);
        return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, result);
    }

    /// <summary>
    /// Aktualizuje regułę AuthZEN i synchronizuje zmianę w API REST OpenFGA.
    /// </summary>
    [HttpPut("rules/{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthZenRuleSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthZenRuleSyncResult>> UpdateRule(
        string id,
        [FromBody] AuthZenRuleDto rule,
        CancellationToken cancellationToken)
    {
        var existing = await _store.GetRuleByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound(new { error = $"Nie znaleziono reguły o ID '{id}'." });
        }

        var result = await _store.UpdateRuleAsync(id, rule, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Usuwa regułę i wycofuje krotkę relacji z API REST OpenFGA (deletes w POST /stores/{id}/write).
    /// </summary>
    [HttpDelete("rules/{id}")]
    [ProducesResponseType(typeof(AuthZenRuleSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthZenRuleSyncResult>> DeleteRule(string id, CancellationToken cancellationToken)
    {
        var existing = await _store.GetRuleByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound(new { error = $"Nie znaleziono reguły o ID '{id}'." });
        }

        var result = await _store.DeleteRuleAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Wymusza synchronizację pojedynczej reguły z serwerem OpenFGA.
    /// </summary>
    [HttpPost("rules/{id}/sync")]
    [ProducesResponseType(typeof(AuthZenRuleSyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthZenRuleSyncResult>> SyncRule(string id, CancellationToken cancellationToken)
    {
        var result = await _store.SyncRuleAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Wymusza hurtową synchronizację wszystkich reguł AuthZEN do API REST OpenFGA.
    /// </summary>
    [HttpPost("rules/sync-all")]
    [ProducesResponseType(typeof(AuthZenRuleSyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthZenRuleSyncResult>> SyncAll(CancellationToken cancellationToken)
    {
        var result = await _store.SyncAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Testuje uprawnienie za pomocą zapytania autoryzacyjnego Check w OpenFGA (POST /stores/{id}/check).
    /// </summary>
    [HttpPost("check")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FgaCheckResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FgaCheckResponse>> CheckAccess(
        [FromBody] FgaCheckRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _fgaClient.CheckAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pobiera status połączenia z lokalnym serwerem OpenFGA (0.0.0.0:8080 / localhost:8080).
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(FgaServerStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<FgaServerStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _store.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Pobiera surowe krotki relacji zapisane bezpośrednio w OpenFGA (POST /stores/{id}/read).
    /// </summary>
    [HttpGet("tuples")]
    [ProducesResponseType(typeof(IReadOnlyList<FgaTupleKey>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FgaTupleKey>>> GetTuples(
        [FromQuery] string? user = null,
        [FromQuery] string? relation = null,
        [FromQuery] string? obj = null,
        CancellationToken cancellationToken = default)
    {
        var tuples = await _fgaClient.ReadTuplesAsync(user, relation, obj, cancellationToken);
        return Ok(tuples);
    }

    /// <summary>
    /// Pobiera listę magazynów (Stores) z OpenFGA (GET /stores).
    /// </summary>
    [HttpGet("stores")]
    [ProducesResponseType(typeof(IReadOnlyList<FgaStore>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FgaStore>>> GetStores(CancellationToken cancellationToken)
    {
        var stores = await _fgaClient.ListStoresAsync(cancellationToken);
        return Ok(stores);
    }
}
