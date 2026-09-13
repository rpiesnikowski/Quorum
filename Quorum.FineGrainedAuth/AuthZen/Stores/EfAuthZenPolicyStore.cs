using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.FineGrainedAuth.AuthZen.Data;
using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Stores;

public class EfAuthZenPolicyStore : IAuthZenPolicyStore
{
    private readonly IAuthZenDbContext _context;
    private readonly ILogger<EfAuthZenPolicyStore> _logger;

    public EfAuthZenPolicyStore(
        IAuthZenDbContext context,
        ILogger<EfAuthZenPolicyStore> logger)
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
        var entity = await _context.AuthZenPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task<(bool Success, string? Error)> CreatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default)
    {
        if (await _context.AuthZenPolicies.AnyAsync(p => p.Name == model.Name, cancellationToken))
        {
            return (false, $"Polityka o nazwie '{model.Name}' już istnieje.");
        }

        var entity = new AuthZenPolicy
        {
            Name = model.Name,
            Description = model.Description,
            IsEnabled = model.IsEnabled,
            Priority = model.Priority,
            Effect = model.Effect,
            SubjectType = model.SubjectType,
            SubjectRoles = model.SubjectRoles,
            SubjectRequiredClaims = model.SubjectRequiredClaims,
            RequireActiveUser = model.RequireActiveUser,
            RequireAuthenticated = model.RequireAuthenticated,
            Action = model.Action,
            ResourceType = model.ResourceType,
            ResourcePattern = model.ResourcePattern,
            ConditionExpression = model.ConditionExpression,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuthZenPolicies.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        _logger.LogInformation("[AuthZEN PAP] Utworzono nową politykę autoryzacyjną: '{Name}' (ID: {Id})", entity.Name, entity.Id);
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

        if (await _context.AuthZenPolicies.AnyAsync(p => p.Name == model.Name && p.Id != model.Id, cancellationToken))
        {
            return (false, $"Inna polityka o nazwie '{model.Name}' już istnieje.");
        }

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.IsEnabled = model.IsEnabled;
        entity.Priority = model.Priority;
        entity.Effect = model.Effect;
        entity.SubjectType = model.SubjectType;
        entity.SubjectRoles = model.SubjectRoles;
        entity.SubjectRequiredClaims = model.SubjectRequiredClaims;
        entity.RequireActiveUser = model.RequireActiveUser;
        entity.RequireAuthenticated = model.RequireAuthenticated;
        entity.Action = model.Action;
        entity.ResourceType = model.ResourceType;
        entity.ResourcePattern = model.ResourcePattern;
        entity.ConditionExpression = model.ConditionExpression;
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
