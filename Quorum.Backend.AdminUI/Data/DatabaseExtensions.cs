using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Quorum.Backend.AdminUI.Data;

public static class DatabaseExtensions
{
    public static void ConfigureDatabase<TContext>(
        this DbContextOptionsBuilder builder,
        IConfiguration configuration, Type program) where TContext : DbContext
    {
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var migrationsAssembly = program.GetTypeInfo().Assembly.GetName().Name;

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
