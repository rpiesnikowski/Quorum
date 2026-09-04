using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace Quorum.Backend.EntityFramework.Data;

#pragma warning disable EF1001 // Microsoft.EntityFrameworkCore.Design.Internal API usage

/// <summary>
/// Opcje konfiguracyjne dla inspekcji i inżynierii wstecznej bazy danych.
/// </summary>
public class DbContextInspectorOptions
{
    /// <summary>
    /// Czy stosować oryginalne nazwy tabel i kolumn z bazy danych (true), czy konwencję PascalCase C# (false).
    /// </summary>
    public bool UseDatabaseNames { get; set; } = false;

    /// <summary>
    /// Czy wyłączyć automatyczną liczbę mnogą dla nazw encji.
    /// </summary>
    public bool NoPluralize { get; set; } = false;

    /// <summary>
    /// Opcjonalna lista tabel do uwzględnienia w analizie.
    /// </summary>
    public IReadOnlyList<string>? Tables { get; set; }

    /// <summary>
    /// Opcjonalna lista schematów do uwzględnienia w analizie.
    /// </summary>
    public IReadOnlyList<string>? Schemas { get; set; }
}

/// <summary>
/// Rezultat porównania schematu modelu z bazą danych lub porównania dwóch fizycznych baz danych.
/// </summary>
public class DbInspectionResult
{
    public bool HasDifferences => Operations.Count > 0;
    public int OperationsCount => Operations.Count;
    public IReadOnlyList<MigrationOperation> Operations { get; init; } = Array.Empty<MigrationOperation>();
    public IReadOnlyList<string> Summary { get; init; } = Array.Empty<string>();
    public string SqlScript { get; init; } = string.Empty;
    public IReadOnlyList<string> SourceTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TargetTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingTablesInTarget { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExtraTablesInTarget { get; init; } = Array.Empty<string>();

    public override string ToString() => SqlScript;
}

/// <summary>
/// Narzędzie inspekcji i porównywania schematów baz danych w czasie wykonywania dla Entity Framework Core.
/// Umożliwia:
/// 1. Porównanie modelu zdefiniowanego w kodzie C# (DbContext) z fizyczną bazą danych.
/// 2. Porównanie dwóch różnych fizycznych baz danych (np. referencyjnej bazy źródłowej z docelową).
/// </summary>
public static class DbContextInspector
{
    /// <summary>
    /// Oryginalna metoda: generuje skrypt migracyjny SQL wyrównujący fizyczną bazę danych do aktualnego modelu C#.
    /// Zwraca czytelny skrypt SQL lub komentarz o pełnej zgodności.
    /// </summary>
    public static string InspectAndGenerateDiffScript<TContext>(TContext context, string targetDatabaseConnectionString) 
        where TContext : DbContext
    {
        var result = CompareModelWithDatabase(context, targetDatabaseConnectionString);
        return result.SqlScript;
    }

    /// <summary>
    /// Porównuje aktualny model C# z fizyczną bazą danych pod wskazanym connection stringiem.
    /// Zwraca obiekt <see cref="DbInspectionResult"/> z listą operacji, podsumowaniem i skryptem SQL.
    /// </summary>
    public static DbInspectionResult CompareModelWithDatabase<TContext>(
        TContext context, 
        string targetDatabaseConnectionString,
        DbContextInspectorOptions? options = null) 
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabaseConnectionString);

        options ??= new DbContextInspectorOptions();

        var internalServiceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        var designTimeServiceProvider = GetDesignTimeServiceProvider(context);

        // 1. Pobieramy model z kodu C# (z zachowaniem metadanych migracyjnych)
        var codeModel = internalServiceProvider.GetService<IDesignTimeModel>()?.Model 
                        ?? context.Model;

        // 2. Inżynieria wsteczna (Scaffolding w pamięci) z fizycznej docelowej bazy danych
        var databaseModelFactory = designTimeServiceProvider.GetRequiredService<IDatabaseModelFactory>();
        var scaffoldModelFactory = designTimeServiceProvider.GetRequiredService<IScaffoldingModelFactory>();

        var factoryOptions = new DatabaseModelFactoryOptions(
            tables: options.Tables ?? Array.Empty<string>(), 
            schemas: options.Schemas ?? Array.Empty<string>()
        );

        var targetDatabaseModel = databaseModelFactory.Create(targetDatabaseConnectionString, factoryOptions);
        var targetPhysicalModel = scaffoldModelFactory.Create(targetDatabaseModel, new ModelReverseEngineerOptions
        {
            UseDatabaseNames = options.UseDatabaseNames,
            NoPluralize = options.NoPluralize
        });

        // 3. Obliczenie różnic (target vs source/codeModel)
        var modelDiffer = internalServiceProvider.GetRequiredService<IMigrationsModelDiffer>();
        var diffOperations = modelDiffer.GetDifferences(
            targetPhysicalModel.GetRelationalModel(), 
            codeModel.GetRelationalModel()
        );

        // Tabele
        var codeTableNames = codeModel.GetEntityTypes()
            .Select(t => t.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetTableNames = targetDatabaseModel.Tables
            .Select(t => t.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingInTarget = codeTableNames
            .Except(targetTableNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extraInTarget = targetTableNames
            .Except(codeTableNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = GenerateSummary(diffOperations);

        string sqlScript;
        if (!diffOperations.Any())
        {
            sqlScript = "-- Fizyczna baza danych jest w pełni zgodna z modelem C#. Brak zmian do wdrożenia.";
        }
        else
        {
            var sqlGenerator = internalServiceProvider.GetRequiredService<IMigrationsSqlGenerator>();
            var sqlCommands = sqlGenerator.Generate(diffOperations, codeModel);
            sqlScript = BuildSqlScript(diffOperations, sqlCommands, summary, "Model C# (DbContext)", targetDatabaseConnectionString);
        }

        return new DbInspectionResult
        {
            Operations = diffOperations,
            Summary = summary,
            SqlScript = sqlScript,
            SourceTables = codeTableNames,
            TargetTables = targetTableNames,
            MissingTablesInTarget = missingInTarget,
            ExtraTablesInTarget = extraInTarget
        };
    }

    /// <summary>
    /// Porównuje dwie fizyczne bazy danych (źródłową i docelową) w kontekście schematu danego DbContext.
    /// Generuje operacje i skrypt SQL potrzebny do zaktualizowania bazy docelowej, aby odpowiadała źródłowej.
    /// </summary>
    public static DbInspectionResult CompareDatabases<TContext>(
        TContext context, 
        string sourceDatabaseConnectionString, 
        string targetDatabaseConnectionString,
        DbContextInspectorOptions? options = null) 
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabaseConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabaseConnectionString);

        options ??= new DbContextInspectorOptions();

        var internalServiceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        var designTimeServiceProvider = GetDesignTimeServiceProvider(context);

        var databaseModelFactory = designTimeServiceProvider.GetRequiredService<IDatabaseModelFactory>();
        var scaffoldModelFactory = designTimeServiceProvider.GetRequiredService<IScaffoldingModelFactory>();
        var modelDiffer = internalServiceProvider.GetRequiredService<IMigrationsModelDiffer>();
        var sqlGenerator = internalServiceProvider.GetRequiredService<IMigrationsSqlGenerator>();

        var factoryOptions = new DatabaseModelFactoryOptions(
            tables: options.Tables ?? Array.Empty<string>(), 
            schemas: options.Schemas ?? Array.Empty<string>()
        );

        // 1. Scaffolding bazy źródłowej (Source)
        var sourceDatabaseModel = databaseModelFactory.Create(sourceDatabaseConnectionString, factoryOptions);
        var sourcePhysicalModel = scaffoldModelFactory.Create(sourceDatabaseModel, new ModelReverseEngineerOptions
        {
            UseDatabaseNames = options.UseDatabaseNames,
            NoPluralize = options.NoPluralize
        });

        // 2. Scaffolding bazy docelowej (Target)
        var targetDatabaseModel = databaseModelFactory.Create(targetDatabaseConnectionString, factoryOptions);
        var targetPhysicalModel = scaffoldModelFactory.Create(targetDatabaseModel, new ModelReverseEngineerOptions
        {
            UseDatabaseNames = options.UseDatabaseNames,
            NoPluralize = options.NoPluralize
        });

        // 3. Obliczenie różnic (co zmienić w Target, aby stał się Source)
        var diffOperations = modelDiffer.GetDifferences(
            targetPhysicalModel.GetRelationalModel(), 
            sourcePhysicalModel.GetRelationalModel()
        );

        var sourceTableNames = sourceDatabaseModel.Tables.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var targetTableNames = targetDatabaseModel.Tables.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var missingInTarget = sourceTableNames.Except(targetTableNames, StringComparer.OrdinalIgnoreCase).ToList();
        var extraInTarget = targetTableNames.Except(sourceTableNames, StringComparer.OrdinalIgnoreCase).ToList();

        var summary = GenerateSummary(diffOperations);

        string sqlScript;
        if (!diffOperations.Any())
        {
            sqlScript = "-- Obie fizyczne bazy danych są w pełni zgodne. Brak różnic w schemacie.";
        }
        else
        {
            var sqlCommands = sqlGenerator.Generate(diffOperations, sourcePhysicalModel);
            sqlScript = BuildSqlScript(diffOperations, sqlCommands, summary, sourceDatabaseConnectionString, targetDatabaseConnectionString);
        }

        return new DbInspectionResult
        {
            Operations = diffOperations,
            Summary = summary,
            SqlScript = sqlScript,
            SourceTables = sourceTableNames,
            TargetTables = targetTableNames,
            MissingTablesInTarget = missingInTarget,
            ExtraTablesInTarget = extraInTarget
        };
    }

    /// <summary>
    /// Pomocnicza metoda generująca skrypt SQL porównujący dwie fizyczne bazy danych.
    /// </summary>
    public static string InspectAndGenerateDatabasesDiffScript<TContext>(
        TContext context, 
        string sourceDatabaseConnectionString, 
        string targetDatabaseConnectionString) 
        where TContext : DbContext
    {
        var result = CompareDatabases(context, sourceDatabaseConnectionString, targetDatabaseConnectionString);
        return result.SqlScript;
    }

    private static IServiceProvider GetDesignTimeServiceProvider(DbContext context)
    {
        var assembly = context.GetType().Assembly;
        var startupAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var builder = new DesignTimeServicesBuilder(
            assembly,
            startupAssembly,
            new OperationReporter(null),
            Array.Empty<string>());

        return builder.Build(context);
    }

    private static List<string> GenerateSummary(IReadOnlyList<MigrationOperation> operations)
    {
        var summary = new List<string>();
        foreach (var op in operations)
        {
            switch (op)
            {
                case CreateTableOperation ct:
                    var colCount = ct.Columns.Count;
                    summary.Add($"[+ Tabela] Utworzenie tabeli '{ct.Name}' ({colCount} kolumn: {string.Join(", ", ct.Columns.Take(5).Select(c => c.Name))}{(colCount > 5 ? "..." : "")})");
                    break;
                case DropTableOperation dt:
                    summary.Add($"[- Tabela] Usunięcie tabeli '{dt.Name}'");
                    break;
                case AddColumnOperation ac:
                    summary.Add($"[+ Kolumna] Dodanie kolumny '{ac.Table}.{ac.Name}' ({ac.ClrType?.Name ?? ac.ColumnType})");
                    break;
                case DropColumnOperation dc:
                    summary.Add($"[- Kolumna] Usunięcie kolumny '{dc.Table}.{dc.Name}'");
                    break;
                case AlterColumnOperation alc:
                    summary.Add($"[~ Kolumna] Zmiana kolumny '{alc.Table}.{alc.Name}' ({alc.ClrType?.Name ?? alc.ColumnType})");
                    break;
                case CreateIndexOperation ci:
                    summary.Add($"[+ Indeks] Utworzenie indeksu '{ci.Name}' na tabeli '{ci.Table}'");
                    break;
                case DropIndexOperation di:
                    summary.Add($"[- Indeks] Usunięcie indeksu '{di.Name}' z tabeli '{di.Table}'");
                    break;
                case AddForeignKeyOperation afk:
                    summary.Add($"[+ Klucz obcy] Dodanie klucza obcego '{afk.Name}' ({afk.Table} -> {afk.PrincipalTable})");
                    break;
                case DropForeignKeyOperation dfk:
                    summary.Add($"[- Klucz obcy] Usunięcie klucza obcego '{dfk.Name}' z tabeli '{dfk.Table}'");
                    break;
                case AddPrimaryKeyOperation apk:
                    summary.Add($"[+ Klucz główny] Dodanie klucza głównego '{apk.Name}' na tabeli '{apk.Table}'");
                    break;
                case DropPrimaryKeyOperation dpk:
                    summary.Add($"[- Klucz główny] Usunięcie klucza głównego '{dpk.Name}' z tabeli '{dpk.Table}'");
                    break;
                case RenameTableOperation rt:
                    summary.Add($"[~ Tabela] Zmiana nazwy tabeli '{rt.Name}' na '{rt.NewName}'");
                    break;
                case RenameColumnOperation rc:
                    summary.Add($"[~ Kolumna] Zmiana nazwy kolumny '{rc.Table}.{rc.Name}' na '{rc.NewName}'");
                    break;
                default:
                    summary.Add($"[Operacja] {op.GetType().Name}");
                    break;
            }
        }
        return summary;
    }

    private static string BuildSqlScript(
        IReadOnlyList<MigrationOperation> diffOperations,
        IReadOnlyList<MigrationCommand> sqlCommands,
        IReadOnlyList<string> summary,
        string sourceDesc,
        string targetDesc)
    {
        var sqlScript = new StringBuilder();
        sqlScript.AppendLine($"-- ============================================================================");
        sqlScript.AppendLine($"-- SKRYPT MIGRACYJNY WYGENEROWANY AUTOMATYCZNIE PRZEZ DbContextInspector");
        sqlScript.AppendLine($"-- Data wygenerowania: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sqlScript.AppendLine($"-- Źródło (Source):     {sourceDesc}");
        sqlScript.AppendLine($"-- Cel (Target):        {targetDesc}");
        sqlScript.AppendLine($"-- Liczba wykrytych operacji: {diffOperations.Count}");
        sqlScript.AppendLine($"-- ============================================================================");
        sqlScript.AppendLine();

        if (summary.Count > 0)
        {
            sqlScript.AppendLine("-- PODSUMOWANIE WYKRYTYCH ZMIAN:");
            foreach (var item in summary)
            {
                sqlScript.AppendLine($"--   * {item}");
            }
            sqlScript.AppendLine();
        }

        sqlScript.AppendLine("-- POLECENIA SQL DLA DOCELOWEJ BAZY DANYCH:");
        foreach (var command in sqlCommands)
        {
            sqlScript.AppendLine(command.CommandText);
            if (!command.CommandText.TrimEnd().EndsWith(";"))
            {
                sqlScript.AppendLine(";");
            }
            sqlScript.AppendLine();
        }

        return sqlScript.ToString();
    }
}
#pragma warning restore EF1001
