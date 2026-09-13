using Microsoft.EntityFrameworkCore;
using Quorum.FineGrainedAuth.AuthZen.Models;

namespace Quorum.FineGrainedAuth.AuthZen.Data;

/// <summary>
/// Abstrakcja kontekstu bazy danych dla silnika PDP i panelu zarządzania PAP w standardzie AuthZEN.
/// </summary>
public interface IAuthZenDbContext
{
    DbSet<AuthZenPolicy> AuthZenPolicies { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedykowany kontekst bazy danych dla modułu Fine-Grained Authorization (AuthZEN & OpenFGA).
/// Może działać niezależnie z SQLite, PostgreSQL, SQL Server lub In-Memory.
/// </summary>
public class AuthZenDbContext : DbContext, IAuthZenDbContext
{
    public AuthZenDbContext(DbContextOptions<AuthZenDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuthZenPolicy> AuthZenPolicies { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuthZenPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Effect).IsRequired().HasMaxLength(16).HasDefaultValue("Permit");
            entity.Property(e => e.Action).IsRequired().HasMaxLength(128).HasDefaultValue("*");
            entity.Property(e => e.ResourceType).HasMaxLength(64).HasDefaultValue("route");
            entity.Property(e => e.ResourcePattern).IsRequired().HasMaxLength(255).HasDefaultValue("*");
            entity.Property(e => e.SubjectType).HasMaxLength(64).HasDefaultValue("user");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasDefaultValue(0);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.IsEnabled);
        });
    }
}
