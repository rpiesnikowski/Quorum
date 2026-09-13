using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.EntityFramework.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IFederationDbContext, IGatewayDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Tabela konfiguracji dynamicznych dostawców tożsamości OpenID Connect (OIDC).
    /// </summary>
    public DbSet<OidcFederationProvider> FederationProviders { get; set; } = null!;

    /// <summary>
    /// Tabela konfiguracji tras i reguł proxy API Gateway.
    /// </summary>
    public DbSet<GatewayRoute> GatewayRoutes { get; set; } = null!;

    /// <summary>
    /// Tabela mapująca zakresy (Scopes) przypisane do tras API Gateway.
    /// </summary>
    public DbSet<GatewayRouteScope> GatewayRouteScopes { get; set; } = null!;

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

        builder.Entity<GatewayRoute>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchPattern).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AddressHost).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Scheme).HasMaxLength(16).HasDefaultValue("https");
            entity.Property(e => e.AddressPort).HasDefaultValue(443);
            entity.Property(e => e.AddressBasePath).HasMaxLength(255);
            entity.Property(e => e.AddressPath).HasMaxLength(255);
            entity.Property(e => e.AddressQueryString).HasMaxLength(500);
            entity.Property(e => e.HttpMethods).HasMaxLength(64).HasDefaultValue("ALL");
            entity.Property(e => e.AuthenticationSchemes).HasMaxLength(255).HasDefaultValue("Bearer");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasDefaultValue(0);
            entity.Property(e => e.AllowAnonymous).HasDefaultValue(false);
            entity.Property(e => e.RequiredScope).HasDefaultValue(false);
            entity.Property(e => e.ScopeName).HasMaxLength(500);

            entity.HasIndex(e => e.MatchPattern);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.IsEnabled);
        });

        builder.Entity<GatewayRouteScope>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Scope).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => new { e.GatewayRouteId, e.Scope });
            entity.HasOne(e => e.GatewayRoute)
                  .WithMany(r => r.Scopes)
                  .HasForeignKey(e => e.GatewayRouteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
