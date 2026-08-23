using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.AdminUI.Data;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.Models;

namespace Quorum.Backend.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IFederationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Tabela konfiguracji dynamicznych dostawców tożsamości OpenID Connect (OIDC).
    /// </summary>
    public DbSet<OidcFederationProvider> FederationProviders { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<OidcFederationProvider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Scheme).IsUnique();
            entity.Property(e => e.Scheme).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Authority).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CallbackPath).IsRequired().HasMaxLength(256);
        });
    }
}
