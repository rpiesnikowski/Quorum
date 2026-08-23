using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Quorum.Backend.Data;

public static class DatabaseExtensions
{
    public static void ConfigureDatabase<TContext>(
        this DbContextOptionsBuilder builder,
        IConfiguration configuration) where TContext : DbContext
    {
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var migrationsAssembly = typeof(Program).GetTypeInfo().Assembly.GetName().Name;

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                var sqliteConn = configuration.GetConnectionString("Sqlite") ?? "Data Source=identityserver.db";
                builder.UseSqlite(sqliteConn, sql => sql.MigrationsAssembly(migrationsAssembly));
                break;

            case "postgresql":
            case "postgres":
            case "npgsql":
                var pgConn = configuration.GetConnectionString("PostgreSQL");
                if (string.IsNullOrWhiteSpace(pgConn))
                {
                    throw new InvalidOperationException("Brak ConnectionStrings:PostgreSQL w appsettings.json!");
                }
                builder.UseNpgsql(pgConn, sql => sql.MigrationsAssembly(migrationsAssembly));
                break;

            default:
                throw new InvalidOperationException($"Niewspierany dostawca bazy danych: {provider}. Wybierz 'Sqlite' lub 'PostgreSQL'.");
        }
    }
}
