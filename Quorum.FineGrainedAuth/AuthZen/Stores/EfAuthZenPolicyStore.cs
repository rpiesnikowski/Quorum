using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quorum.FineGrainedAuth.AuthZen.Data;
using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Stores;

public class EfAuthZenPolicyStore : IAuthZenPolicyStore
{
    private readonly IAuthZenDbContext? _context;
    private readonly ILogger<EfAuthZenPolicyStore> _logger;

    private static readonly List<AuthZenPolicy> _inMemoryFallbackList = new()
    {
        new AuthZenPolicy
        {
            Id = 1,
            Name = "Zezwolenie Odczytu API dla Użytkowników",
            Description = "Domyślna polityka AuthZEN zezwalająca uwierzytelnionym użytkownikom na operacje odczytu GET",
            SubjectType = "user",
            SubjectRoles = "User,Admin",
            Action = "GET",
            ResourceType = "route",
            ResourcePattern = "/api/*",
            Effect = "Permit",
            IsEnabled = true,
            Priority = 10,
            CreatedAt = DateTime.UtcNow
        },
        new AuthZenPolicy
        {
            Id = 2,
            Name = "Pełny Dostęp Administratora (SuperUser)",
            Description = "Domyślna polityka AuthZEN nadająca roli Admin pełne uprawnienia do wszystkich zasobów i akcji",
            SubjectType = "role",
            SubjectRoles = "Admin",
            Action = "*",
            ResourceType = "*",
            ResourcePattern = "*",
            Effect = "Permit",
            IsEnabled = true,
            Priority = 100,
            CreatedAt = DateTime.UtcNow
        }
    };

    public EfAuthZenPolicyStore(
        IAuthZenDbContext context,
        ILogger<EfAuthZenPolicyStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public EfAuthZenPolicyStore(
        ILogger<EfAuthZenPolicyStore> logger)
    {
        _context = null;
        _logger = logger;
    }

    public async Task<PagedResult<AuthZenPolicyAdminModel>> GetPoliciesAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd odpytania bazy danych IAuthZenDbContext. Użycie magazynu In-Memory.");
            }
        }

        // Fallback in-memory
        lock (_inMemoryFallbackList)
        {
            var memQuery = _inMemoryFallbackList.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                memQuery = memQuery.Where(p =>
                    p.Name.ToLower().Contains(s) ||
                    (p.Description != null && p.Description.ToLower().Contains(s)) ||
                    p.ResourcePattern.ToLower().Contains(s) ||
                    p.Action.ToLower().Contains(s) ||
                    (p.SubjectRoles != null && p.SubjectRoles.ToLower().Contains(s)));
            }

            var total = memQuery.Count();
            var items = memQuery
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToModel)
                .ToList();

            return Task.FromResult(new PagedResult<AuthZenPolicyAdminModel>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            }).Result;
        }
    }

    public async Task<AuthZenPolicyAdminModel?> GetPolicyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
            {
                var entity = await _context.AuthZenPolicies.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (entity != null)
                {
                    return MapToModel(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd odczytu polityki z IAuthZenDbContext.");
            }
        }

        lock (_inMemoryFallbackList)
        {
            var mem = _inMemoryFallbackList.FirstOrDefault(p => p.Id == id);
            return mem == null ? null : MapToModel(mem);
        }
    }

    public async Task<(bool Success, string? Error)> CreatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd zapisu polityki do IAuthZenDbContext. Zapisywanie do pamięci.");
            }
        }

        lock (_inMemoryFallbackList)
        {
            if (_inMemoryFallbackList.Any(p => p.Name == model.Name))
            {
                return (false, $"Polityka o nazwie '{model.Name}' już istnieje.");
            }

            var nextId = _inMemoryFallbackList.Count > 0 ? _inMemoryFallbackList.Max(p => p.Id) + 1 : 1;
            var memEntity = new AuthZenPolicy
            {
                Id = nextId,
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
            _inMemoryFallbackList.Add(memEntity);
            model.Id = nextId;
            return (true, null);
        }
    }

    public async Task<(bool Success, string? Error)> UpdatePolicyAsync(AuthZenPolicyAdminModel model, CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd aktualizacji polityki w IAuthZenDbContext.");
            }
        }

        lock (_inMemoryFallbackList)
        {
            var mem = _inMemoryFallbackList.FirstOrDefault(p => p.Id == model.Id);
            if (mem == null)
            {
                return (false, $"Polityka o ID {model.Id} nie została znaleziona.");
            }

            mem.Name = model.Name;
            mem.Description = model.Description;
            mem.IsEnabled = model.IsEnabled;
            mem.Priority = model.Priority;
            mem.Effect = model.Effect;
            mem.SubjectType = model.SubjectType;
            mem.SubjectRoles = model.SubjectRoles;
            mem.SubjectRequiredClaims = model.SubjectRequiredClaims;
            mem.RequireActiveUser = model.RequireActiveUser;
            mem.RequireAuthenticated = model.RequireAuthenticated;
            mem.Action = model.Action;
            mem.ResourceType = model.ResourceType;
            mem.ResourcePattern = model.ResourcePattern;
            mem.ConditionExpression = model.ConditionExpression;
            mem.UpdatedAt = DateTime.UtcNow;

            return (true, null);
        }
    }

    public async Task<(bool Success, string? Error)> DeletePolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
            {
                var entity = await _context.AuthZenPolicies
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (entity != null)
                {
                    _context.AuthZenPolicies.Remove(entity);
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("[AuthZEN PAP] Usunięto politykę autoryzacyjną: '{Name}' (ID: {Id})", entity.Name, id);
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd usunięcia polityki z IAuthZenDbContext.");
            }
        }

        lock (_inMemoryFallbackList)
        {
            var mem = _inMemoryFallbackList.FirstOrDefault(p => p.Id == id);
            if (mem != null)
            {
                _inMemoryFallbackList.Remove(mem);
            }
            return (true, null);
        }
    }

    public async Task<(bool Success, string? Error)> TogglePolicyStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_context != null)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthZEN PAP] Błąd zmiany statusu w IAuthZenDbContext.");
            }
        }

        lock (_inMemoryFallbackList)
        {
            var mem = _inMemoryFallbackList.FirstOrDefault(p => p.Id == id);
            if (mem == null)
            {
                return (false, $"Polityka o ID {id} nie została znaleziona.");
            }

            mem.IsEnabled = !mem.IsEnabled;
            mem.UpdatedAt = DateTime.UtcNow;
            return (true, null);
        }
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
