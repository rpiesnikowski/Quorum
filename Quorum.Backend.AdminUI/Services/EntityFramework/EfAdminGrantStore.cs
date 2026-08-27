using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminGrantStore : IAdminGrantStore
{
    private readonly PersistedGrantDbContext _context;

    public EfAdminGrantStore(PersistedGrantDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PersistedGrantAdminModel>> GetGrantsAsync(
        string? search = null,
        string? type = null,
        string? clientId = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PersistedGrants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(g =>
                g.Key.ToLower().Contains(s) ||
                (g.SubjectId != null && g.SubjectId.ToLower().Contains(s)) ||
                g.ClientId.ToLower().Contains(s) ||
                (g.Description != null && g.Description.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(g => g.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            query = query.Where(g => g.ClientId == clientId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(g => g.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(g => new PersistedGrantAdminModel
        {
            Key = g.Key,
            Type = g.Type,
            SubjectId = g.SubjectId,
            SessionId = g.SessionId,
            ClientId = g.ClientId,
            Description = g.Description,
            CreationTime = g.CreationTime,
            Expiration = g.Expiration,
            ConsumedTime = g.ConsumedTime,
            Data = g.Data
        }).ToList();

        return new PagedResult<PersistedGrantAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<PersistedGrantAdminModel?> GetGrantByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var g = await _context.PersistedGrants.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (g == null) return null;

        return new PersistedGrantAdminModel
        {
            Key = g.Key,
            Type = g.Type,
            SubjectId = g.SubjectId,
            SessionId = g.SessionId,
            ClientId = g.ClientId,
            Description = g.Description,
            CreationTime = g.CreationTime,
            Expiration = g.Expiration,
            ConsumedTime = g.ConsumedTime,
            Data = g.Data
        };
    }

    public async Task<(bool Success, string? Error)> RevokeGrantAsync(string key, CancellationToken cancellationToken = default)
    {
        var entity = await _context.PersistedGrants.FirstOrDefaultAsync(g => g.Key == key, cancellationToken);
        if (entity == null) return (true, null);

        _context.PersistedGrants.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RevokeAllForSubjectAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        var grants = await _context.PersistedGrants.Where(g => g.SubjectId == subjectId).ToListAsync(cancellationToken);
        if (grants.Count > 0)
        {
            _context.PersistedGrants.RemoveRange(grants);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RevokeAllForClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var grants = await _context.PersistedGrants.Where(g => g.ClientId == clientId).ToListAsync(cancellationToken);
        if (grants.Count > 0)
        {
            _context.PersistedGrants.RemoveRange(grants);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return (true, null);
    }
}
