using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Quorum.Backend.AdminUI2.Models;
using Quorum.Backend.AdminUI2.Services.Interfaces;

namespace Quorum.Backend.AdminUI2.Services.EntityFramework;

public class EfAdminApiScopeStore : IAdminApiScopeStore
{
    private readonly ConfigurationDbContext _context;

    public EfAdminApiScopeStore(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ApiScopeAdminModel>> GetScopesAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ApiScopes
            .Include(s => s.UserClaims)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(sc => sc.Name.ToLower().Contains(s) || (sc.DisplayName != null && sc.DisplayName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(sc => sc.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(e => new ApiScopeAdminModel
        {
            Id = e.Id,
            Name = e.Name,
            DisplayName = e.DisplayName,
            Description = e.Description,
            Required = e.Required,
            Emphasize = e.Emphasize,
            ShowInDiscoveryDocument = e.ShowInDiscoveryDocument,
            Enabled = e.Enabled,
            UserClaims = e.UserClaims?.Select(c => c.Type).ToList() ?? new()
        }).ToList();

        return new PagedResult<ApiScopeAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<ApiScopeAdminModel?> GetScopeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiScopes
            .Include(s => s.UserClaims)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entity == null) return null;

        return new ApiScopeAdminModel
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Required = entity.Required,
            Emphasize = entity.Emphasize,
            ShowInDiscoveryDocument = entity.ShowInDiscoveryDocument,
            Enabled = entity.Enabled,
            UserClaims = entity.UserClaims?.Select(c => c.Type).ToList() ?? new()
        };
    }

    public async Task<(bool Success, string? Error)> CreateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default)
    {
        var exists = await _context.ApiScopes.AnyAsync(s => s.Name == model.Name, cancellationToken);
        if (exists)
        {
            return (false, $"Zakres API o nazwie '{model.Name}' już istnieje.");
        }

        var entity = new ApiScope
        {
            Name = model.Name,
            DisplayName = model.DisplayName,
            Description = model.Description,
            Required = model.Required,
            Emphasize = model.Emphasize,
            ShowInDiscoveryDocument = model.ShowInDiscoveryDocument,
            Enabled = model.Enabled
        };

        if (model.UserClaims != null)
        {
            foreach (var claim in model.UserClaims)
            {
                entity.UserClaims.Add(new ApiScopeClaim { Type = claim });
            }
        }

        _context.ApiScopes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateScopeAsync(ApiScopeAdminModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiScopes
            .Include(s => s.UserClaims)
            .FirstOrDefaultAsync(s => s.Id == model.Id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Zakres o ID {model.Id} nie został znaleziony.");
        }

        entity.Name = model.Name;
        entity.DisplayName = model.DisplayName;
        entity.Description = model.Description;
        entity.Required = model.Required;
        entity.Emphasize = model.Emphasize;
        entity.ShowInDiscoveryDocument = model.ShowInDiscoveryDocument;
        entity.Enabled = model.Enabled;

        entity.UserClaims.Clear();
        foreach (var claim in model.UserClaims ?? new())
        {
            entity.UserClaims.Add(new ApiScopeClaim { Type = claim });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteScopeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiScopes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity == null) return (true, null);

        _context.ApiScopes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
