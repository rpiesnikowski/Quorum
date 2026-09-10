using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Data;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

/// <summary>
/// Implementacja magazynu Entity Framework Core dla panelu PAP (Policy Administration Point) standardu AuthZEN.
/// Umożliwia administratorom i deweloperom definiowanie, edytowanie, usuwanie i publikowanie reguł autoryzacyjnych.
/// </summary>
public class EfAdminAuthZenPolicyStore : IAdminAuthZenPolicyStore
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EfAdminAuthZenPolicyStore> _logger;

    public EfAdminAuthZenPolicyStore(
        ApplicationDbContext context,
        ILogger<EfAdminAuthZenPolicyStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<AuthZenPolicyAdminModel>> GetPoliciesAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuthZenPolicies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(s) ||
                (p.Description != null && p.Description.ToLower().Contains(s)) ||
                p.ResourcePattern.ToLower().Contains(s) ||
                p.Action.ToLower().Contains(s) ||
                (p.SubjectRoles != null && p.SubjectRoles.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(MapToModel).ToList();

        return new PagedResult<AuthZenPolicyAdminModel>
        {
            Items = list,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuthZenPolicyAdminModel?> GetPolicyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.AuthZenPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<(bool Success, string? Error)> CreatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default)
    {
        var existing = await _context.AuthZenPolicies
            .AnyAsync(p => p.Name.ToLower() == model.Name.ToLower(), cancellationToken);

        if (existing)
        {
            return (false, $"Polityka o nazwie '{model.Name}' już istnieje.");
        }

        var entity = new AuthZenPolicy
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            Effect = model.Effect.Trim(),
            SubjectType = string.IsNullOrWhiteSpace(model.SubjectType) ? "user" : model.SubjectType.Trim(),
            SubjectRoles = model.SubjectRoles?.Trim(),
            SubjectRequiredClaims = model.SubjectRequiredClaims?.Trim(),
            RequireActiveUser = model.RequireActiveUser,
            RequireAuthenticated = model.RequireAuthenticated,
            Action = string.IsNullOrWhiteSpace(model.Action) ? "*" : model.Action.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(model.ResourceType) ? "route" : model.ResourceType.Trim(),
            ResourcePattern = string.IsNullOrWhiteSpace(model.ResourcePattern) ? "*" : model.ResourcePattern.Trim(),
            ConditionExpression = model.ConditionExpression?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuthZenPolicies.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        _logger.LogInformation("[AuthZEN PAP] Utworzono nową politykę autoryzacyjną: '{Name}' (ID: {Id}, Efekt: {Effect})",
            entity.Name, entity.Id, entity.Effect);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.AuthZenPolicies
            .FirstOrDefaultAsync(p => p.Id == model.Id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Polityka o ID {model.Id} nie została znaleziona.");
        }

        var nameExists = await _context.AuthZenPolicies
            .AnyAsync(p => p.Name.ToLower() == model.Name.ToLower() && p.Id != model.Id, cancellationToken);

        if (nameExists)
        {
            return (false, $"Inna polityka o nazwie '{model.Name}' już istnieje.");
        }

        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim();
        entity.IsEnabled = model.IsEnabled;
        entity.Priority = model.Priority;
        entity.Effect = model.Effect.Trim();
        entity.SubjectType = string.IsNullOrWhiteSpace(model.SubjectType) ? "user" : model.SubjectType.Trim();
        entity.SubjectRoles = model.SubjectRoles?.Trim();
        entity.SubjectRequiredClaims = model.SubjectRequiredClaims?.Trim();
        entity.RequireActiveUser = model.RequireActiveUser;
        entity.RequireAuthenticated = model.RequireAuthenticated;
        entity.Action = string.IsNullOrWhiteSpace(model.Action) ? "*" : model.Action.Trim();
        entity.ResourceType = string.IsNullOrWhiteSpace(model.ResourceType) ? "route" : model.ResourceType.Trim();
        entity.ResourcePattern = string.IsNullOrWhiteSpace(model.ResourcePattern) ? "*" : model.ResourcePattern.Trim();
        entity.ConditionExpression = model.ConditionExpression?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[AuthZEN PAP] Zaktualizowano politykę autoryzacyjną: '{Name}' (ID: {Id})", entity.Name, entity.Id);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeletePolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.AuthZenPolicies
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            return (true, null);
        }

        _context.AuthZenPolicies.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AuthZEN PAP] Usunięto politykę autoryzacyjną: '{Name}' (ID: {Id})", entity.Name, id);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> TogglePolicyStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.AuthZenPolicies
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Polityka o ID {id} nie została znaleziona.");
        }

        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[AuthZEN PAP] Zmiana statusu polityki '{Name}' (ID: {Id}) na IsEnabled={IsEnabled}",
            entity.Name, id, entity.IsEnabled);

        return (true, null);
    }

    private static AuthZenPolicyAdminModel MapToModel(AuthZenPolicy entity)
    {
        return new AuthZenPolicyAdminModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            Priority = entity.Priority,
            Effect = entity.Effect,
            SubjectType = entity.SubjectType,
            SubjectRoles = entity.SubjectRoles,
            SubjectRequiredClaims = entity.SubjectRequiredClaims,
            RequireActiveUser = entity.RequireActiveUser,
            RequireAuthenticated = entity.RequireAuthenticated,
            Action = entity.Action,
            ResourceType = entity.ResourceType,
            ResourcePattern = entity.ResourcePattern,
            ConditionExpression = entity.ConditionExpression,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
