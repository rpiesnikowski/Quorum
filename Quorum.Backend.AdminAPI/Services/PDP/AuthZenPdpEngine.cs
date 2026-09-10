using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminAPI.Services.PIP;
using Quorum.Backend.EntityFramework.AuthZen;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminAPI.Services.PDP;

/// <summary>
/// Domyślny silnik decyzyjny PDP (Policy Decision Point) w standardzie AuthZEN 1.0.
/// Integruje bazę polityk autoryzacyjnych (PAP) z danymi tożsamości pobieranymi w locie z PIP (Identity / Claims).
/// </summary>
public class AuthZenPdpEngine : IAuthZenPolicyDecisionPoint
{
    private readonly IAuthZenDbContext _dbContext;
    private readonly IAuthZenPolicyInformationPoint _pip;
    private readonly ILogger<AuthZenPdpEngine> _logger;

    public AuthZenPdpEngine(
        IAuthZenDbContext dbContext,
        IAuthZenPolicyInformationPoint pip,
        ILogger<AuthZenPdpEngine> logger)
    {
        _dbContext = dbContext;
        _pip = pip;
        _logger = logger;
    }

    public async Task<AuthZenEvaluationResponse> EvaluateAsync(
        AuthZenEvaluationRequest request, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N");

        _logger.LogInformation("[AuthZEN PDP] Rozpoczęto ewaluację żądania {RequestId}: Subject='{Subject}', Action='{Action}', Resource='{Resource}'",
            requestId, request.Subject.Id, request.Action.Name, request.Resource.Id);

        // 1. Konsultacja z PIP (Policy Information Point) - pobranie danych użytkownika z AspNetCoreIdentity
        AuthZenSubjectAttributes? pipAttributes = null;
        if (!string.IsNullOrWhiteSpace(request.Subject.Id) && !request.Subject.Id.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
        {
            pipAttributes = await _pip.GetSubjectAttributesAsync(request.Subject.Id, cancellationToken);
            await _pip.EnrichSubjectAsync(request.Subject, cancellationToken);
        }

        // 2. Pobranie aktywnych polityk autoryzacyjnych posortowanych według priorytetu malejąco
        var policies = await _dbContext.AuthZenPolicies
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        // 3. Sprawdzenie braku polityk (Fallback bezpieczny: domyślna odmowa)
        if (policies.Count == 0)
        {
            stopwatch.Stop();
            _logger.LogWarning("[AuthZEN PDP] Brak zdefiniowanych aktywnych polityk w bazie PAP. Domyślna odmowa.");
            return new AuthZenEvaluationResponse
            {
                Decision = false,
                Context = new AuthZenEvaluationResponseContext
                {
                    Id = requestId,
                    Reason = "Brak aktywnych reguł autoryzacji w bazie PAP (Default Deny)",
                    Evaluator = "Quorum AuthZEN PDP Engine",
                    Timestamp = DateTimeOffset.UtcNow
                }
            };
        }

        // 4. Ewaluacja kolejnych reguł w kolejności priorytetu
        foreach (var policy in policies)
        {
            if (!IsResourceMatch(policy, request.Resource))
            {
                continue;
            }

            if (!IsActionMatch(policy, request.Action))
            {
                continue;
            }

            if (!IsSubjectMatch(policy, request.Subject, pipAttributes))
            {
                continue;
            }

            // Znaleziono pasującą politykę!
            stopwatch.Stop();
            var isPermit = policy.Effect.Equals("Permit", StringComparison.OrdinalIgnoreCase);

            var summaryAttributes = new Dictionary<string, object?>();
            if (pipAttributes != null)
            {
                summaryAttributes["user_name"] = pipAttributes.UserName;
                summaryAttributes["is_active"] = pipAttributes.IsActive;
                summaryAttributes["roles"] = pipAttributes.Roles;
            }

            var decisionReason = isPermit
                ? $"Dostęp przyznany przez regułę '{policy.Name}' (Priorytet: {policy.Priority})"
                : $"Dostęp zablokowany przez regułę odmowy (Deny) '{policy.Name}' (Priorytet: {policy.Priority})";

            if (!string.IsNullOrWhiteSpace(policy.Description))
            {
                decisionReason += $": {policy.Description}";
            }

            _logger.LogInformation("[AuthZEN PDP] Żądanie {RequestId} zakończone w {ElapsedMs}ms. Decyzja={Decision}, Polityka='{Policy}', Powód: {Reason}",
                requestId, stopwatch.ElapsedMilliseconds, isPermit, policy.Name, decisionReason);

            return new AuthZenEvaluationResponse
            {
                Decision = isPermit,
                Context = new AuthZenEvaluationResponseContext
                {
                    Id = requestId,
                    Decision = isPermit ? "permit" : "deny",
                    Reason = decisionReason,
                    PolicyId = policy.Name,
                    Evaluator = "Quorum AuthZEN PDP Engine",
                    Timestamp = DateTimeOffset.UtcNow,
                    AttributesEvaluated = summaryAttributes
                }
            };
        }

        // 5. Żadna reguła nie dopasowała żądania -> Domyślna odmowa (Default Deny)
        stopwatch.Stop();
        _logger.LogInformation("[AuthZEN PDP] Żądanie {RequestId} zakończone w {ElapsedMs}ms. Brak pasującej reguły zezwalającej -> Decyzja=False",
            requestId, stopwatch.ElapsedMilliseconds);

        return new AuthZenEvaluationResponse
        {
            Decision = false,
            Context = new AuthZenEvaluationResponseContext
            {
                Id = requestId,
                Reason = "Żadna z aktywnych reguł autoryzacji nie zezwoliła na wykonanie tej operacji (Domyślna odmowa - Default Deny)",
                Evaluator = "Quorum AuthZEN PDP Engine",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    public async Task<AuthZenBatchEvaluationResponse> EvaluateBatchAsync(
        AuthZenBatchEvaluationRequest request, 
        CancellationToken cancellationToken = default)
    {
        var batchResponse = new AuthZenBatchEvaluationResponse();

        foreach (var singleReq in request.Evaluations)
        {
            var res = await EvaluateAsync(singleReq, cancellationToken);
            batchResponse.Evaluations.Add(res);
        }

        return batchResponse;
    }

    // --- Metody pomocnicze dopasowania reguł ---

    private static bool IsResourceMatch(AuthZenPolicy policy, AuthZenResource resource)
    {
        // Sprawdzenie typu zasobu (np. "route", "api", "*")
        if (!string.IsNullOrWhiteSpace(policy.ResourceType) && !policy.ResourceType.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            if (!policy.ResourceType.Equals(resource.Type, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Sprawdzenie wzorca identyfikatora zasobu (np. "/api/orders", "^/api/v1/.*", "*")
        if (string.IsNullOrWhiteSpace(policy.ResourcePattern) || policy.ResourcePattern.Equals("*"))
        {
            return true;
        }

        var pattern = policy.ResourcePattern.Trim();
        var target = (resource.Id ?? string.Empty).Trim();

        // 1. Dokładne dopasowanie
        if (target.Equals(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Prefiks z gwiazdką wildcard (np. /api/orders/*)
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 3. Wyrażenie regularne Regex (np. ^/api/orders/.*)
        if (pattern.StartsWith("^") || pattern.Contains(".*"))
        {
            try
            {
                if (Regex.IsMatch(target, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
                {
                    return true;
                }
            }
            catch
            {
                // W przypadku błędnego regex ignoruj
            }
        }

        return false;
    }

    private static bool IsActionMatch(AuthZenPolicy policy, AuthZenAction action)
    {
        if (string.IsNullOrWhiteSpace(policy.Action) || policy.Action.Equals("*"))
        {
            return true;
        }

        var actions = policy.Action.Split(new[] { ',', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
        return actions.Any(a => a.Equals(action.Name, StringComparison.OrdinalIgnoreCase) || a.Equals("*"));
    }

    private static bool IsSubjectMatch(
        AuthZenPolicy policy, 
        AuthZenSubject subject, 
        AuthZenSubjectAttributes? pipAttributes)
    {
        // 1. Sprawdzenie typu podmiotu
        if (!string.IsNullOrWhiteSpace(policy.SubjectType) && !policy.SubjectType.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            if (!policy.SubjectType.Equals(subject.Type, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // 2. Sprawdzenie wymogu uwierzytelnienia
        if (policy.RequireAuthenticated)
        {
            var isAuth = pipAttributes != null 
                || (subject.Properties != null 
                    && subject.Properties.TryGetValue("is_authenticated", out var authObj) 
                    && authObj is bool b && b);

            if (!isAuth && (string.IsNullOrWhiteSpace(subject.Id) || subject.Id.Equals("anonymous", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // 3. Sprawdzenie wymogu aktywnego konta (niezablokowane w Identity)
        if (policy.RequireActiveUser && pipAttributes != null)
        {
            if (pipAttributes.IsLockedOut || !pipAttributes.IsActive)
            {
                return false;
            }
        }

        // 4. Sprawdzenie ról użytkownika (OR - wystarczy posiadanie jednej z wymienionych ról)
        if (!string.IsNullOrWhiteSpace(policy.SubjectRoles))
        {
            var requiredRoles = policy.SubjectRoles
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (requiredRoles.Length > 0)
            {
                var userRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (pipAttributes != null)
                {
                    foreach (var r in pipAttributes.Roles) userRoles.Add(r);
                }

                // Sprawdź także properties z samego subject
                if (subject.Properties != null && subject.Properties.TryGetValue("roles", out var subRolesObj))
                {
                    if (subRolesObj is IEnumerable<string> rList)
                    {
                        foreach (var r in rList) userRoles.Add(r);
                    }
                    else if (subRolesObj is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in jsonElem.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrEmpty(s)) userRoles.Add(s);
                        }
                    }
                }

                var hasAnyRole = requiredRoles.Any(reqRole => userRoles.Contains(reqRole));
                if (!hasAnyRole)
                {
                    return false;
                }
            }
        }

        // 5. Sprawdzenie wymaganych claims (np. {"department":"Finance"})
        if (!string.IsNullOrWhiteSpace(policy.SubjectRequiredClaims))
        {
            if (!CheckClaimsMatch(policy.SubjectRequiredClaims, pipAttributes, subject))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CheckClaimsMatch(
        string requiredClaimsString, 
        AuthZenSubjectAttributes? pipAttributes, 
        AuthZenSubject subject)
    {
        var userClaims = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (pipAttributes != null)
        {
            foreach (var (k, v) in pipAttributes.Claims)
            {
                userClaims[k] = new List<string>(v);
            }
        }

        // Dodaj claims z subject.Properties["claims"] jeśli są
        if (subject.Properties != null && subject.Properties.TryGetValue("claims", out var claimsObj))
        {
            if (claimsObj is IDictionary<string, object?> dict)
            {
                foreach (var (k, v) in dict)
                {
                    if (!userClaims.TryGetValue(k, out var list))
                    {
                        list = new List<string>();
                        userClaims[k] = list;
                    }
                    if (v != null) list.Add(v.ToString() ?? "");
                }
            }
        }

        // Próba sparsowania JSON
        try
        {
            using var doc = JsonDocument.Parse(requiredClaimsString);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var claimType = prop.Name;
                    var expectedVal = prop.Value.GetString();

                    if (!userClaims.TryGetValue(claimType, out var values))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(expectedVal) && !values.Any(v => v.Equals(expectedVal, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
        catch
        {
            // Parsowanie formatu "klucz=wartość,klucz2=wartość2"
            var pairs = requiredClaimsString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                var key = parts[0].Trim();
                var expected = parts.Length > 1 ? parts[1].Trim() : null;

                if (!userClaims.TryGetValue(key, out var values))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(expected) && !values.Any(v => v.Equals(expected, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
