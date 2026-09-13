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
    public AuthZenDbContext()
    {
    }

    public AuthZenDbContext(DbContextOptions<AuthZenDbContext> options)
        : base(options)
    {
    }

    public AuthZenDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<AuthZenPolicy> AuthZenPolicies { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=authzen_policies.db");
        }
    }

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

            // Dane początkowe (Seed)
            entity.HasData(
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
                    Priority = 10
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
                    Priority = 100
                }
            );
        });
    }
}
