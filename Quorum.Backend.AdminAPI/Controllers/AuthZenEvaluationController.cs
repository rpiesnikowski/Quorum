using Microsoft.AspNetCore.Mvc;
using Quorum.Backend.AdminAPI.Services.PDP;
using Quorum.Backend.AdminAPI.Services.PIP;
using Quorum.Backend.EntityFramework.AuthZen;

namespace Quorum.Backend.AdminAPI.Controllers;

/// <summary>
/// Kontroler REST API dla silnika decyzyjnego PDP (Policy Decision Point)
/// zgodny ze specyfikacją OpenID Foundation AuthZEN 1.0.
/// Udostępnia standardowe punkty końcowe /access/v1/evaluation i /access/v1/evaluations.
/// </summary>
[ApiController]
[Produces("application/json")]
public class AuthZenEvaluationController : ControllerBase
{
    private readonly IAuthZenPolicyDecisionPoint _pdp;
    private readonly IAuthZenPolicyInformationPoint _pip;
    private readonly ILogger<AuthZenEvaluationController> _logger;

    public AuthZenEvaluationController(
        IAuthZenPolicyDecisionPoint pdp,
        IAuthZenPolicyInformationPoint pip,
        ILogger<AuthZenEvaluationController> logger)
    {
        _pdp = pdp;
        _pip = pip;
        _logger = logger;
    }

    /// <summary>
    /// Główny endpoint ewaluacyjny AuthZEN 1.0 (POST /access/v1/evaluation).
    /// Odbiera zapytanie od PEP (Policy Enforcement Point) i zwraca decyzję boolean z kontekstem.
    /// </summary>
    [HttpPost("access/v1/evaluation")]
    [HttpPost("api/authzen/evaluation")]
    [ProducesResponseType(typeof(AuthZenEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthZenEvaluationResponse>> Evaluate(
        [FromBody] AuthZenEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.Subject == null || request.Action == null || request.Resource == null)
        {
            return BadRequest(new { error = "invalid_request", message = "Wymagane pola subject, action i resource nie mogą być puste." });
        }

        var response = await _pdp.EvaluateAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Wsadowy endpoint ewaluacyjny AuthZEN 1.0 (POST /access/v1/evaluations).
    /// </summary>
    [HttpPost("access/v1/evaluations")]
    [HttpPost("api/authzen/evaluations")]
    [ProducesResponseType(typeof(AuthZenBatchEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthZenBatchEvaluationResponse>> EvaluateBatch(
        [FromBody] AuthZenBatchEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.Evaluations == null || request.Evaluations.Count == 0)
        {
            return BadRequest(new { error = "invalid_request", message = "Lista ewaluacji nie może być pusta." });
        }

        var response = await _pdp.EvaluateBatchAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Szybka weryfikacja przez query params (GET /access/v1/evaluation).
    /// Przydatna do szybkich testów integracyjnych i diagnostyki.
    /// </summary>
    [HttpGet("access/v1/evaluation")]
    [HttpGet("api/authzen/evaluation")]
    [ProducesResponseType(typeof(AuthZenEvaluationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthZenEvaluationResponse>> EvaluateGet(
        [FromQuery] string subject,
        [FromQuery] string action = "GET",
        [FromQuery] string resource = "*",
        [FromQuery] string? subjectType = "user",
        [FromQuery] string? resourceType = "route",
        CancellationToken cancellationToken = default)
    {
        var request = new AuthZenEvaluationRequest
        {
            Subject = new AuthZenSubject { Id = subject ?? "anonymous", Type = subjectType ?? "user" },
            Action = new AuthZenAction { Name = action },
            Resource = new AuthZenResource { Id = resource, Type = resourceType ?? "route" }
        };

        var response = await _pdp.EvaluateAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Endpoint diagnostyczny PIP: zwraca pobrane atrybuty tożsamości z AspNetCoreIdentity dla danego SubjectId.
    /// </summary>
    [HttpGet("api/authzen/pip/subject/{subjectId}")]
    [ProducesResponseType(typeof(AuthZenSubjectAttributes), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthZenSubjectAttributes>> GetPipSubject(
        string subjectId, 
        CancellationToken cancellationToken)
    {
        var attributes = await _pip.GetSubjectAttributesAsync(subjectId, cancellationToken);
        if (attributes == null)
        {
            return NotFound(new { message = $"Podmiot '{subjectId}' nie został odnaleziony w PIP (AspNetCoreIdentity)." });
        }

        return Ok(attributes);
    }
}
