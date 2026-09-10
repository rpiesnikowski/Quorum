using Microsoft.EntityFrameworkCore;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.EntityFramework.Data;

/// <summary>
/// Abstrakcja kontekstu bazy danych dla silnika PDP i panelu zarządzania PAP w standardzie AuthZEN.
/// </summary>
public interface IAuthZenDbContext
{
    DbSet<AuthZenPolicy> AuthZenPolicies { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
