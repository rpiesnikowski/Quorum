using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Quorum.Backend.EntityFramework.AuthZen;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminAPI.Services.PIP;

/// <summary>
/// Implementacja PIP (Policy Information Point) oparta o AspNetCoreIdentity i bazę danych użytkowników.
/// Pobiera z bazy tożsamości role, stan konta (Lockout) oraz claimsy użytkownika (dział, uprawnienia, atrybuty).
/// </summary>
public class AuthZenIdentityPipService<TUser> : IAuthZenPolicyInformationPoint
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly ILogger<AuthZenIdentityPipService<TUser>> _logger;

    public AuthZenIdentityPipService(
        UserManager<TUser> userManager,
        ILogger<AuthZenIdentityPipService<TUser>> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<AuthZenSubjectAttributes?> GetSubjectAttributesAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            // 1. Wyszukanie użytkownika w magazynie Identity po ID, Username lub Email
            var user = await _userManager.FindByIdAsync(subjectId)
                       ?? await _userManager.FindByNameAsync(subjectId)
                       ?? await _userManager.FindByEmailAsync(subjectId);

            if (user == null)
            {
                _logger.LogDebug("[AuthZEN PIP] Użytkownik '{SubjectId}' nie został odnaleziony w AspNetCoreIdentity.", subjectId);
                return null;
            }

            // 2. Pobranie ról użytkownika z Identity
            var roles = await _userManager.GetRolesAsync(user);

            // 3. Pobranie claimsów użytkownika (np. department, permission, tenant_id)
            var identityClaims = await _userManager.GetClaimsAsync(user);
            var claimsDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var claim in identityClaims)
            {
                if (!claimsDict.TryGetValue(claim.Type, out var list))
                {
                    list = new List<string>();
                    claimsDict[claim.Type] = list;
                }
                list.Add(claim.Value);
            }

            // Dodaj role do claims jeśli jeszcze ich tam nie ma
            if (!claimsDict.ContainsKey("role"))
            {
                claimsDict["role"] = roles.ToList();
            }

            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            var isActive = !isLocked;

            return new AuthZenSubjectAttributes
            {
                SubjectId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                IsLockedOut = isLocked,
                IsActive = isActive,
                Roles = roles.ToList(),
                Claims = claimsDict
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthZEN PIP] Błąd podczas pobierania atrybutów dla podmiotu '{SubjectId}' z AspNetCoreIdentity.", subjectId);
            return null;
        }
    }

    public async Task EnrichSubjectAsync(AuthZenSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject == null || string.IsNullOrWhiteSpace(subject.Id)) return;

        subject.Properties ??= new Dictionary<string, object?>();

        var attributes = await GetSubjectAttributesAsync(subject.Id, cancellationToken);
        if (attributes != null)
        {
            subject.Properties["pip_resolved"] = true;
            subject.Properties["user_id"] = attributes.SubjectId;
            subject.Properties["username"] = attributes.UserName;
            subject.Properties["email"] = attributes.Email;
            subject.Properties["email_confirmed"] = attributes.EmailConfirmed;
            subject.Properties["is_active"] = attributes.IsActive;
            subject.Properties["is_locked_out"] = attributes.IsLockedOut;
            subject.Properties["roles"] = attributes.Roles;

            // Płaskie claimsy dla szybkiej ewaluacji
            var flatClaims = new Dictionary<string, string>();
            foreach (var (k, values) in attributes.Claims)
            {
                flatClaims[k] = string.Join(",", values);
            }
            subject.Properties["identity_claims"] = flatClaims;
        }
        else
        {
            subject.Properties["pip_resolved"] = false;
        }
    }
}
