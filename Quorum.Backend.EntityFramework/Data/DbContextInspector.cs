using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace Quorum.Backend.EntityFramework.Data;

public class DbContextInspector
{
    public static string InspectAndGenerateDiffScript<TContext>(TContext context, string targetDatabaseConnectionString) 
        where TContext : DbContext
    {
        var serviceProvider = context.GetService<IInfrastructure<IServiceProvider>>().Instance;

        // 1. Pobieramy model zdefiniowany w kodzie C#
        var designTimeModel = serviceProvider.GetRequiredService<IDesignTimeModel>();
        var codeModel = designTimeModel.Model;

        // 2. Budujemy model z FIZYCZNEJ bazy danych (Scaffolding / Reverse Engineering w pamięci)
        var databaseModelFactory = serviceProvider.GetRequiredService<IDatabaseModelFactory>();
        var scaffoldModelFactory = serviceProvider.GetRequiredService<IScaffoldingModelFactory>();

        // Pobieramy surowy schemat bazy (tabele, kolumny, klucze)
        var databaseModel = databaseModelFactory.Create(
            targetDatabaseConnectionString, 
            new DatabaseModelFactoryOptions()
        );

        // Przekształcamy surowy schemat bazy w obiekt IModel rozumiany przez EF Core
        var physicalDatabaseModel = scaffoldModelFactory.Create(databaseModel, new ModelReverseEngineerOptions
        {
            UseDatabaseNames = false, // false = stosuje konwencje nazewnicze C# (PascalCase)
            NoPluralize = false
        });

        // 3. Porównujemy model z kodu C# z modelem z fizycznej bazy
        var modelDiffer = serviceProvider.GetRequiredService<IMigrationsModelDiffer>();

        // GetDifferences(source, target) -> co trzeba zmienić w target (baza), aby odpowiadał source (kod)
        IReadOnlyList<MigrationOperation> diffOperations = modelDiffer.GetDifferences(
            physicalDatabaseModel.GetRelationalModel(), 
            codeModel.GetRelationalModel()
        );

        if (!diffOperations.Any())
        {
            return "-- Fizyczna baza danych jest w pełni zgodna z modelem C#. Brak zmian do wdrożenia.";
        }

        // 4. Generujemy skrypt SQL dla wybranego silnika bazy
        var sqlGenerator = serviceProvider.GetRequiredService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> sqlCommands = sqlGenerator.Generate(diffOperations, codeModel);

        var sqlScript = new StringBuilder();
        sqlScript.AppendLine($"-- SKRYPT MIGRACYJNY WYGENEROWANY AUTOMATYCZNIE: {DateTime.Now}");
        sqlScript.AppendLine($"-- Liczba Wykrytych Operacji: {diffOperations.Count}\n");

        foreach (var command in sqlCommands)
        {
            sqlScript.AppendLine(command.CommandText);
            sqlScript.AppendLine(";");
        }

        return sqlScript.ToString();
    }
}