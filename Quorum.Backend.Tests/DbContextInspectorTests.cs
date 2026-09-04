using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Options;
using Quorum.Backend.EntityFramework.Data;
using Xunit;
using FluentAssertions;

namespace Quorum.Backend.Tests;

/// <summary>
/// Testy integracyjne dla DbContextInspector sprawdzające:
/// 1. Porównanie aktualnych modeli C# z fizyczną bazą danych (ApplicationDbContext, ConfigurationDbContext, PersistedGrantDbContext).
/// 2. Porównanie dwóch różnych fizycznych baz danych (baza źródłowa vs baza docelowa) dla każdego z kontekstów.
/// </summary>
public class DbContextInspectorIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public DbContextInspectorIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Quorum_Inspector_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignorujemy błędy zwalniania plików tymczasowych po testach
        }
    }

    private string CreateDbPath(string prefix) => Path.Combine(_tempDirectory, $"{prefix}_{Guid.NewGuid():N}.db");
    private static string ToConnectionString(string path) => $"Data Source={path}";

    #region 1. ApplicationDbContext Tests

    [Fact]
    public void ApplicationDbContext_Model_Vs_PhysicalDatabase_Detects_Tables_And_Generates_Report()
    {
        // Arrange
        var dbPath = CreateDbPath("app_model_vs_db");
        var connStr = ToConnectionString(dbPath);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connStr)
            .Options;

        // Utworzenie fizycznej bazy danych
        using (var db = new ApplicationDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        // Act
        using (var db = new ApplicationDbContext(options))
        {
            var result = DbContextInspector.CompareModelWithDatabase(db, connStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.Should().NotBeNull();
            result.SourceTables.Should().Contain(new[] { "AspNetUsers", "AspNetRoles", "GatewayRoutes", "GatewayRouteScopes", "FederationProviders" });
            result.TargetTables.Should().Contain(new[] { "AspNetUsers", "AspNetRoles", "GatewayRoutes", "GatewayRouteScopes", "FederationProviders" });
            result.MissingTablesInTarget.Should().BeEmpty();
        }
    }

    [Fact]
    public void ApplicationDbContext_Model_Vs_EmptyDatabase_Generates_Complete_MigrationScript()
    {
        // Arrange
        var dbPath = CreateDbPath("app_empty");
        var connStr = ToConnectionString(dbPath);

        // Tworzymy pusty plik SQLite bez tabel
        using (var conn = new SqliteConnection(connStr))
        {
            conn.Open();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connStr)
            .Options;

        // Act
        using (var db = new ApplicationDbContext(options))
        {
            var result = DbContextInspector.CompareModelWithDatabase(db, connStr);

            // Assert
            result.HasDifferences.Should().BeTrue();
            result.Operations.Should().NotBeEmpty();
            result.Operations.OfType<CreateTableOperation>().Should().NotBeEmpty();

            // Powinny być wykryte brakujące tabele
            result.MissingTablesInTarget.Should().Contain(new[] { "AspNetUsers", "GatewayRoutes", "FederationProviders" });

            // Skrypt SQL powinien zawierać polecenia tworzenia tabel
            result.SqlScript.Should().Contain("CREATE TABLE");
            result.SqlScript.Should().Contain("GatewayRoutes");
            result.Summary.Should().Contain(s => s.Contains("GatewayRoutes"));
        }
    }

    [Fact]
    public void ApplicationDbContext_Compare_Two_Different_Databases_Detects_Discrepancies()
    {
        // Arrange
        // Baza źródłowa (pełny schemat)
        var sourceDbPath = CreateDbPath("app_source");
        var sourceConnStr = ToConnectionString(sourceDbPath);
        var optionsSource = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sourceConnStr).Options;

        using (var dbSource = new ApplicationDbContext(optionsSource))
        {
            dbSource.Database.EnsureCreated();
        }

        // Baza docelowa (usunięto tabelę GatewayRoutes i GatewayRouteScopes - symulacja starszej wersji)
        var targetDbPath = CreateDbPath("app_target");
        var targetConnStr = ToConnectionString(targetDbPath);
        var optionsTarget = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(targetConnStr).Options;

        using (var dbTarget = new ApplicationDbContext(optionsTarget))
        {
            dbTarget.Database.EnsureCreated();
        }

        using (var conn = new SqliteConnection(targetConnStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE GatewayRouteScopes; DROP TABLE GatewayRoutes;";
            cmd.ExecuteNonQuery();
        }

        // Act: Porównujemy dwie bazy za pomocą ApplicationDbContext
        using (var db = new ApplicationDbContext(optionsSource))
        {
            var result = DbContextInspector.CompareDatabases(db, sourceConnStr, targetConnStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.HasDifferences.Should().BeTrue();
            result.MissingTablesInTarget.Should().Contain(new[] { "GatewayRoutes", "GatewayRouteScopes" });
            result.TargetTables.Should().NotContain("GatewayRoutes");
            result.SourceTables.Should().Contain("GatewayRoutes");

            var createTableOps = result.Operations.OfType<CreateTableOperation>().ToList();
            createTableOps.Should().Contain(t => t.Name == "GatewayRoutes");

            result.SqlScript.Should().Contain("GatewayRoutes");
            result.Summary.Should().Contain(s => s.Contains("GatewayRoutes"));
        }
    }

    [Fact]
    public void ApplicationDbContext_Compare_Identical_Databases_Reports_No_Differences()
    {
        // Arrange
        var db1Path = CreateDbPath("app_identical_1");
        var db2Path = CreateDbPath("app_identical_2");
        var connStr1 = ToConnectionString(db1Path);
        var connStr2 = ToConnectionString(db2Path);

        var opts1 = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connStr1).Options;
        var opts2 = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connStr2).Options;

        using (var db1 = new ApplicationDbContext(opts1)) db1.Database.EnsureCreated();
        using (var db2 = new ApplicationDbContext(opts2)) db2.Database.EnsureCreated();

        // Act
        using var db = new ApplicationDbContext(opts1);
        var result = DbContextInspector.CompareDatabases(db, connStr1, connStr2, new DbContextInspectorOptions
        {
            UseDatabaseNames = true
        });

        // Assert
        result.HasDifferences.Should().BeFalse();
        result.Operations.Should().BeEmpty();
        result.MissingTablesInTarget.Should().BeEmpty();
        result.SqlScript.Should().Contain("Brak różnic w schemacie");
    }

    #endregion

    #region 2. OpenIdentity ConfigurationDbContext Tests

    [Fact]
    public void ConfigurationDbContext_Model_Vs_PhysicalDatabase_Inspects_IdentityServer_Tables()
    {
        // Arrange
        var dbPath = CreateDbPath("cfg_model_vs_db");
        var connStr = ToConnectionString(dbPath);

        var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseSqlite(connStr)
            .Options;
        var storeOptions = new ConfigurationStoreOptions();

        using (var db = new ConfigurationDbContext(options, storeOptions))
        {
            db.Database.EnsureCreated();
        }

        // Act
        using (var db = new ConfigurationDbContext(options, storeOptions))
        {
            var result = DbContextInspector.CompareModelWithDatabase(db, connStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.Should().NotBeNull();
            result.SourceTables.Should().Contain(new[] { "Clients", "ApiResources", "ApiScopes", "IdentityResources" });
            result.TargetTables.Should().Contain(new[] { "Clients", "ApiResources", "ApiScopes", "IdentityResources" });
            result.MissingTablesInTarget.Should().BeEmpty();
        }
    }

    [Fact]
    public void ConfigurationDbContext_Compare_Two_Different_Databases_Detects_Missing_Client_Tables()
    {
        // Arrange
        var sourcePath = CreateDbPath("cfg_source");
        var targetPath = CreateDbPath("cfg_target");
        var sourceConnStr = ToConnectionString(sourcePath);
        var targetConnStr = ToConnectionString(targetPath);

        var optionsSource = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(sourceConnStr).Options;
        var optionsTarget = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(targetConnStr).Options;
        var storeOptions = new ConfigurationStoreOptions();

        using (var db = new ConfigurationDbContext(optionsSource, storeOptions)) db.Database.EnsureCreated();
        using (var db = new ConfigurationDbContext(optionsTarget, storeOptions)) db.Database.EnsureCreated();

        // Symulacja brakujących tabel klientów w bazie docelowej
        using (var conn = new SqliteConnection(targetConnStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DROP TABLE ClientClaims;
                DROP TABLE ClientCorsOrigins;
                DROP TABLE ClientGrantTypes;
                DROP TABLE ClientIdPRestrictions;
                DROP TABLE ClientPostLogoutRedirectUris;
                DROP TABLE ClientProperties;
                DROP TABLE ClientRedirectUris;
                DROP TABLE ClientScopes;
                DROP TABLE ClientSecrets;
                DROP TABLE Clients;
            ";
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var db = new ConfigurationDbContext(optionsSource, storeOptions))
        {
            var result = DbContextInspector.CompareDatabases(db, sourceConnStr, targetConnStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.HasDifferences.Should().BeTrue();
            result.MissingTablesInTarget.Should().Contain("Clients");
            result.Operations.OfType<CreateTableOperation>().Should().Contain(t => t.Name == "Clients");
            result.SqlScript.Should().Contain("Clients");
            result.Summary.Should().Contain(s => s.Contains("Clients"));
        }
    }

    #endregion

    #region 3. OpenIdentity PersistedGrantDbContext Tests

    [Fact]
    public void PersistedGrantDbContext_Model_Vs_PhysicalDatabase_Inspects_Operational_Tables()
    {
        // Arrange
        var dbPath = CreateDbPath("grant_model_vs_db");
        var connStr = ToConnectionString(dbPath);

        var options = new DbContextOptionsBuilder<PersistedGrantDbContext>()
            .UseSqlite(connStr)
            .Options;
        var operationalOptions = new OperationalStoreOptions();

        using (var db = new PersistedGrantDbContext(options, operationalOptions))
        {
            db.Database.EnsureCreated();
        }

        // Act
        using (var db = new PersistedGrantDbContext(options, operationalOptions))
        {
            var result = DbContextInspector.CompareModelWithDatabase(db, connStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.Should().NotBeNull();
            result.SourceTables.Should().Contain(new[] { "PersistedGrants", "DeviceCodes", "Keys", "ServerSideSessions" });
            result.TargetTables.Should().Contain(new[] { "PersistedGrants", "DeviceCodes", "Keys", "ServerSideSessions" });
            result.MissingTablesInTarget.Should().BeEmpty();
        }
    }

    [Fact]
    public void PersistedGrantDbContext_Compare_Two_Different_Databases_Generates_Sync_Script()
    {
        // Arrange
        var sourcePath = CreateDbPath("grant_source");
        var targetPath = CreateDbPath("grant_target");
        var sourceConnStr = ToConnectionString(sourcePath);
        var targetConnStr = ToConnectionString(targetPath);

        var optionsSource = new DbContextOptionsBuilder<PersistedGrantDbContext>().UseSqlite(sourceConnStr).Options;
        var operationalOptions = new OperationalStoreOptions();

        // Źródłowa baza z pełnym schematem
        using (var db = new PersistedGrantDbContext(optionsSource, operationalOptions))
        {
            db.Database.EnsureCreated();
        }

        // Docelowa baza całkowicie pusta
        using (var conn = new SqliteConnection(targetConnStr))
        {
            conn.Open();
        }

        // Act
        using (var db = new PersistedGrantDbContext(optionsSource, operationalOptions))
        {
            var result = DbContextInspector.CompareDatabases(db, sourceConnStr, targetConnStr, new DbContextInspectorOptions
            {
                UseDatabaseNames = true
            });

            // Assert
            result.HasDifferences.Should().BeTrue();
            result.MissingTablesInTarget.Should().Contain(new[] { "PersistedGrants", "DeviceCodes", "Keys", "ServerSideSessions" });
            result.Operations.OfType<CreateTableOperation>().Should().HaveCountGreaterThanOrEqualTo(4);
            result.SqlScript.Should().Contain("CREATE TABLE");
            result.SqlScript.Should().Contain("PersistedGrants");
            result.SqlScript.Should().Contain("Keys");
        }
    }

    #endregion

    #region 4. Helper Script Methods & Backward Compatibility Tests

    [Fact]
    public void InspectAndGenerateDiffScript_Backward_Compatibility_Returns_Script_String()
    {
        // Arrange
        var dbPath = CreateDbPath("compat_test");
        var connStr = ToConnectionString(dbPath);

        using (var conn = new SqliteConnection(connStr))
        {
            conn.Open();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connStr)
            .Options;

        // Act
        using var db = new ApplicationDbContext(options);
        var script = DbContextInspector.InspectAndGenerateDiffScript(db, connStr);

        // Assert
        script.Should().NotBeNullOrWhiteSpace();
        script.Should().Contain("SKRYPT MIGRACYJNY WYGENEROWANY AUTOMATYCZNIE");
        script.Should().Contain("CREATE TABLE");
    }

    [Fact]
    public void InspectAndGenerateDatabasesDiffScript_Returns_Formattable_Migration_Script()
    {
        // Arrange
        var sourcePath = CreateDbPath("script_source");
        var targetPath = CreateDbPath("script_target");
        var sourceConnStr = ToConnectionString(sourcePath);
        var targetConnStr = ToConnectionString(targetPath);

        var optionsSource = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sourceConnStr).Options;
        using (var db = new ApplicationDbContext(optionsSource)) db.Database.EnsureCreated();

        using (var conn = new SqliteConnection(targetConnStr)) conn.Open();

        // Act
        using var dbContext = new ApplicationDbContext(optionsSource);
        var script = DbContextInspector.InspectAndGenerateDatabasesDiffScript(dbContext, sourceConnStr, targetConnStr);

        // Assert
        script.Should().NotBeNullOrWhiteSpace();
        script.Should().Contain("POLECENIA SQL DLA DOCELOWEJ BAZY DANYCH");
        script.Should().Contain("CREATE TABLE");
    }

    #endregion
}
