using Quorum.FineGrainedAuth.AuthZen.Models;
using Quorum.FineGrainedAuth.OpenFGA.Models;
using Quorum.FineGrainedAuth.OpenFGA.Services;

namespace Quorum.FineGrainedAuth.OpenFGA.Adapters;

/// <summary>
/// Mostek (Adapter) mapujący standardowe żądania autoryzacji AuthZEN (OpenID Foundation)
/// na relacyjny model kontroli dostępu OpenFGA (Google Zanzibar ReBAC).
/// </summary>
public class AuthZenToOpenFgaAdapter
{
    private readonly IOpenFgaClient _fgaClient;

    public AuthZenToOpenFgaAdapter(IOpenFgaClient fgaClient)
    {
        _fgaClient = fgaClient;
    }

    /// <summary>
    /// Mapuje AuthZenEvaluationRequest (Subject, Action, Resource) na zapytanie OpenFGA Tuple Check.
    /// Format podmiotu: "{subject.type}:{subject.id}"
    /// Format relacji: "{action.name}"
    /// Format zasobu: "{resource.type}:{resource.id}"
    /// </summary>
    public async Task<AuthZenEvaluationResponse> EvaluateViaFgaAsync(AuthZenEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var subjectUser = $"{request.Subject.Type}:{request.Subject.Id}";
        var actionRelation = request.Action.Name.ToLowerInvariant();
        var targetObject = $"{request.Resource.Type}:{request.Resource.Id}";

        var fgaRequest = new FgaCheckRequest
        {
            TupleKey = new FgaTupleKey
            {
                User = subjectUser,
                Relation = actionRelation,
                Object = targetObject
            },
            Context = request.Context
        };

        var fgaResult = await _fgaClient.CheckAsync(fgaRequest, cancellationToken);

        return new AuthZenEvaluationResponse
        {
            Decision = fgaResult.Allowed,
            Context = new AuthZenEvaluationResponseContext
            {
                Reason = fgaResult.Allowed 
                    ? $"Granted via OpenFGA Zanzibar relationship ({fgaResult.Resolution ?? "allowed"})." 
                    : $"Denied by OpenFGA: subject '{subjectUser}' does not have relation '{actionRelation}' on object '{targetObject}'.",
                Evaluator = "OpenFGA-Zanzibar-Adapter",
                Timestamp = DateTime.UtcNow,
                AttributesEvaluated = new Dictionary<string, object?>
                {
                    ["openfga.user"] = subjectUser,
                    ["openfga.relation"] = actionRelation,
                    ["openfga.object"] = targetObject,
                    ["openfga.allowed"] = fgaResult.Allowed.ToString()
                }
            }
        };
    }
}
