using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Entities;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Services.EntityFramework;

public class EfAdminIdentityResourceStore : IAdminIdentityResourceStore
{
    private readonly ConfigurationDbContext _context;

    public EfAdminIdentityResourceStore(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<IdentityResourceAdminModel>> GetResourcesAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IdentityResources
            .Include(r => r.UserClaims)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(s) || (r.DisplayName != null && r.DisplayName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = entities.Select(e => new IdentityResourceAdminModel
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

        return new PagedResult<IdentityResourceAdminModel>(list, totalCount, page, pageSize);
    }

    public async Task<IdentityResourceAdminModel?> GetResourceByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (entity == null) return null;

        return new IdentityResourceAdminModel
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

    public async Task<(bool Success, string? Error)> CreateResourceAsync(IdentityResourceAdminModel model, CancellationToken cancellationToken = default)
    {
        var exists = await _context.IdentityResources.AnyAsync(r => r.Name == model.Name, cancellationToken);
        if (exists)
        {
            return (false, $"Zasób tożsamości o nazwie '{model.Name}' już istnieje.");
        }

        var entity = new IdentityResource
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
                entity.UserClaims.Add(new IdentityResourceClaim { Type = claim });
            }
        }

        _context.IdentityResources.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        model.Id = entity.Id;
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateResourceAsync(IdentityResourceAdminModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdentityResources
            .Include(r => r.UserClaims)
            .FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);

        if (entity == null)
        {
            return (false, $"Zasób o ID {model.Id} nie został znaleziony.");
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
            entity.UserClaims.Add(new IdentityResourceClaim { Type = claim });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteResourceAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdentityResources.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null) return (true, null);

        _context.IdentityResources.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SeedStandardResourcesAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new List<(string Name, string DisplayName, string[] Claims)>
        {
            ("openid", "Twój unikalny identyfikator tożsamości", new[] { "sub" }),
            ("profile", "Profil użytkownika (imię, nazwisko, preferencje)", new[] { "name", "family_name", "given_name", "middle_name", "nickname", "preferred_username", "profile", "picture", "website", "gender", "birthdate", "zoneinfo", "locale", "updated_at" }),
            ("email", "Twój adres e-mail", new[] { "email", "email_verified" }),
            ("address", "Twój adres pocztowy / zamieszkania", new[] { "address" }),
            ("phone", "Twój numer telefonu", new[] { "phone_number", "phone_number_verified" })
        };

        foreach (var (name, displayName, claims) in defaults)
        {
            var exists = await _context.IdentityResources.AnyAsync(r => r.Name == name, cancellationToken);
            if (!exists)
            {
                var res = new IdentityResource
                {
                    Name = name,
                    DisplayName = displayName,
                    Required = name == "openid",
                    Emphasize = name == "openid" || name == "profile",
                    ShowInDiscoveryDocument = true,
                    Enabled = true
                };
                foreach (var claim in claims)
                {
                    res.UserClaims.Add(new IdentityResourceClaim { Type = claim });
                }
                _context.IdentityResources.Add(res);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
