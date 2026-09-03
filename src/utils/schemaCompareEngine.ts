import { SqlEngine, TableModel, ColumnDefinition, IndexDefinition, TableDiff, SchemaCompareResult, MigrationExecutionStep } from '../types/migrations';
import { EF_CORE_TABLES } from '../data/efCoreSchemaData';

export interface DatabasePreset {
  id: string;
  name: string;
  engine: SqlEngine;
  connectionString: string;
  description: string;
  simulatedExistingTables: Record<string, {
    columns: string[];
    indexes: string[];
  }>;
}

export const PRESET_DATABASES: DatabasePreset[] = [
  {
    id: 'partial-v1',
    name: 'Baza v1.0 (Brak bramki GatewayRoutes i nowych kolumn)',
    engine: 'postgres',
    connectionString: 'Host=localhost;Port=5432;Database=quorum_v1;Username=postgres;Password=SecretPassword123!;SSL Mode=Prefer;',
    description: 'Baza posiada tabele Identity i podstawowych Klientów, ale brakuje w niej tabel GatewayRoutes, GatewayRouteScopes oraz kolumn FullName i CreatedAt.',
    simulatedExistingTables: {
      'AspNetUsers': {
        columns: ['Id', 'UserName', 'NormalizedUserName', 'Email', 'NormalizedEmail', 'EmailConfirmed', 'PasswordHash', 'SecurityStamp', 'ConcurrencyStamp', 'PhoneNumber', 'PhoneNumberConfirmed', 'TwoFactorEnabled', 'LockoutEnd', 'LockoutEnabled', 'AccessFailedCount'],
        indexes: ['EmailIndex', 'UserNameIndex']
      },
      'AspNetRoles': {
        columns: ['Id', 'Name', 'NormalizedName', 'ConcurrencyStamp'],
        indexes: ['RoleNameIndex']
      },
      'AspNetUserRoles': {
        columns: ['UserId', 'RoleId'],
        indexes: ['IX_AspNetUserRoles_RoleId']
      },
      'Clients': {
        columns: ['Id', 'ClientId', 'ClientName', 'Description', 'Enabled', 'ProtocolType', 'RequireClientSecret', 'RequireConsent'],
        indexes: ['IX_Clients_ClientId']
      },
      'ApiScopes': {
        columns: ['Id', 'Name', 'DisplayName', 'Description', 'Enabled'],
        indexes: ['IX_ApiScopes_Name']
      }
    }
  },
  {
    id: 'sqlite-local',
    name: 'Lokalny SQLite (identityserver.db)',
    engine: 'sqlite',
    connectionString: 'Data Source=identityserver.db;Cache=Shared;Foreign Keys=True;',
    description: 'Lokalna baza SQLite z częściowo utworzonymi tabelami tokenów i brakiem indeksów.',
    simulatedExistingTables: {
      'AspNetUsers': {
        columns: ['Id', 'UserName', 'NormalizedUserName', 'Email', 'NormalizedEmail', 'PasswordHash'],
        indexes: ['UserNameIndex']
      },
      'GatewayRoutes': {
        columns: ['Id', 'MatchPattern', 'Scheme', 'AddressHost', 'AddressPort', 'IsEnabled', 'CreatedAt'],
        indexes: []
      },
      'Clients': {
        columns: ['Id', 'ClientId', 'ClientName', 'Enabled', 'ProtocolType', 'Created'],
        indexes: ['IX_Clients_ClientId']
      }
    }
  },
  {
    id: 'sqlserver-dev',
    name: 'SQL Server (Quorum_Development)',
    engine: 'sqlserver',
    connectionString: 'Server=tcp:sqlserver.internal,1433;Initial Catalog=QuorumDb;User ID=sa;Password=DevPassword@2026;TrustServerCertificate=True;',
    description: 'Instancja developerska SQL Server ze starszą wersją schematu tabel federacyjnych.',
    simulatedExistingTables: {
      'AspNetUsers': {
        columns: ['Id', 'UserName', 'NormalizedUserName', 'Email', 'NormalizedEmail', 'EmailConfirmed', 'PasswordHash', 'SecurityStamp', 'ConcurrencyStamp', 'FullName'],
        indexes: ['EmailIndex', 'UserNameIndex']
      },
      'FederationProviders': {
        columns: ['Id', 'Scheme', 'DisplayName', 'Authority', 'ClientId', 'ClientSecret', 'ResponseType', 'Scope', 'CallbackPath'],
        indexes: ['IX_FederationProviders_Scheme']
      },
      'Clients': {
        columns: ['Id', 'ClientId', 'ClientName', 'Description', 'Enabled', 'ProtocolType', 'RequireClientSecret', 'RequireConsent', 'AllowOfflineAccess', 'Created'],
        indexes: ['IX_Clients_ClientId']
      }
    }
  },
  {
    id: 'empty-database',
    name: 'Czysta nowa baza (Empty Schema)',
    engine: 'postgres',
    connectionString: 'Host=postgres.cloud.internal;Port=5432;Database=quorum_fresh;Username=db_admin;Password=prod_secret;',
    description: 'Świeżo założona pusta baza danych. Wszystkie obiekty Entity Framework zostaną wykryte jako brakujące.',
    simulatedExistingTables: {}
  }
];

/**
 * Automatyczne wykrywanie silnika bazy na podstawie connection stringa
 */
export function detectEngineFromConnectionString(connStr: string): SqlEngine {
  const lower = connStr.toLowerCase();
  if (lower.includes('data source=') && (lower.includes('.db') || lower.includes('.sqlite'))) {
    return 'sqlite';
  }
  if (lower.includes('server=') || lower.includes('initial catalog=') || lower.includes('database=') && lower.includes('user id=sa')) {
    return 'sqlserver';
  }
  if (lower.includes('host=') && lower.includes('port=1521') || lower.includes('oracle') || lower.includes('service_name=')) {
    return 'oracle';
  }
  if (lower.includes('host=') || lower.includes('postgres') || lower.includes('npgsql')) {
    return 'postgres';
  }
  return 'postgres';
}

/**
 * Wyciąganie nazwy bazy danych z connection string
 */
export function extractDatabaseName(connStr: string): string {
  const match = connStr.match(/(?:Database|Initial Catalog|Data Source)=([^;]+)/i);
  if (match && match[1]) {
    return match[1].trim();
  }
  return 'Baza Danych';
}

/**
 * Główna funkcja wykonująca Schema Compare (porównanie modeli EF Core z bazą)
 */
export function performSchemaCompare(
  connStr: string,
  engine: SqlEngine,
  currentExistingState?: Record<string, { columns: string[]; indexes: string[] }>
): SchemaCompareResult {
  const dbName = extractDatabaseName(connStr);
  const matchedTables: TableDiff[] = [];

  let missingTablesCount = 0;
  let missingColumnsCount = 0;
  let missingIndexesCount = 0;
  let existingTablesCount = 0;

  // Domyślne mapowanie jeśli użytkownik podał własny string
  const existingMap = currentExistingState || {};

  EF_CORE_TABLES.forEach(efTable => {
    const existingTableInfo = existingMap[efTable.name];

    if (!existingTableInfo) {
      // Tabela w ogóle nie istnieje w bazie danych!
      missingTablesCount++;
      const missingCols = efTable.columns;
      const missingIdxs = efTable.indexes;
      missingColumnsCount += missingCols.length;
      missingIndexesCount += missingIdxs.length;

      matchedTables.push({
        tableName: efTable.name,
        category: efTable.categoryLabel,
        status: 'missing_table',
        existingColumns: [],
        missingColumns: missingCols,
        existingIndexes: [],
        missingIndexes: missingIdxs
      });
    } else {
      // Tabela istnieje - porównaj kolumny i indeksy!
      existingTablesCount++;
      const existingColNames = new Set(existingTableInfo.columns.map(c => c.toLowerCase()));
      const existingIdxNames = new Set(existingTableInfo.indexes.map(i => i.toLowerCase()));

      const missingCols: ColumnDefinition[] = [];
      efTable.columns.forEach(col => {
        if (!existingColNames.has(col.name.toLowerCase())) {
          // Brakująca kolumna w istniejącej tabeli - musi zostać dodana jako NULL!
          missingCols.push(col);
          missingColumnsCount++;
        }
      });

      const missingIdxs: IndexDefinition[] = [];
      efTable.indexes.forEach(idx => {
        if (!existingIdxNames.has(idx.name.toLowerCase())) {
          missingIdxs.push(idx);
          missingIndexesCount++;
        }
      });

      const status = missingCols.length > 0 ? 'has_missing_columns' : 'matched';

      matchedTables.push({
        tableName: efTable.name,
        category: efTable.categoryLabel,
        status,
        existingColumns: existingTableInfo.columns,
        missingColumns: missingCols,
        existingIndexes: existingTableInfo.indexes,
        missingIndexes: missingIdxs
      });
    }
  });

  const totalExpectedTables = EF_CORE_TABLES.length;
  const totalDifferenceCount = missingTablesCount + missingColumnsCount + missingIndexesCount;
  const synchronizedPercentage = totalDifferenceCount === 0
    ? 100
    : Math.max(0, Math.round(((totalExpectedTables - missingTablesCount) / totalExpectedTables) * 100));

  // Generowanie precyzyjnego skryptu Delta Migration SQL
  const deltaSql = generateDeltaMigrationSql(matchedTables, engine);

  return {
    engine,
    connectionString: connStr,
    databaseName: dbName,
    timestamp: new Date().toLocaleTimeString('pl-PL', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
    tables: matchedTables,
    summary: {
      totalExpectedTables,
      existingTablesCount,
      missingTablesCount,
      missingColumnsCount,
      missingIndexesCount,
      synchronizedPercentage
    },
    deltaSql
  };
}

/**
 * Generowanie skryptu Delta na podstawie wyników Schema Compare.
 * ZGODNIE Z WYMOGIEM UŻYTKOWNIKA:
 * "Najważniejsze żeby skrypty generowały się indepodentnie.
 * Czyli jeśli tabela jest to jej nie usuwa, jeśli kolumna jest to jej nie dodaje, etc.
 * Jeśli kolumny nie ma to dodaje NULL-owalną."
 */
function generateDeltaMigrationSql(diffs: TableDiff[], engine: SqlEngine): string {
  let delta = `-- =========================================================================\n`;
  delta += `-- WYNIK PORÓWNANIA SCHEMATÓW (SCHEMA COMPARE DELTA MIGRATION)\n`;
  delta += `-- WYGENEROWANO DLA SILNIKA: ${engine.toUpperCase()}\n`;
  delta += `-- ZASADA: Tylko brakujące obiekty są dodawane (kolumny dodawane jako NULL)\n`;
  delta += `-- =========================================================================\n\n`;

  const missingTables = diffs.filter(d => d.status === 'missing_table');
  const tablesWithMissingCols = diffs.filter(d => d.status === 'has_missing_columns');
  const tablesWithMissingIndexes = diffs.filter(d => d.missingIndexes.length > 0);

  if (missingTables.length === 0 && tablesWithMissingCols.length === 0 && tablesWithMissingIndexes.length === 0) {
    return delta + `-- BAZA DANYCH JEST W 100% ZGODNA ZE SCHEMATEM ENTITY FRAMEWORK.\n-- Brak wymaganych zmian strukturalnych.\n`;
  }

  // 1. Brakujące tabele
  if (missingTables.length > 0) {
    delta += `-- -------------------------------------------------------------\n`;
    delta += `-- ETAP 1: TWORZENIE BRAKUJĄCYCH TABEL (${missingTables.length} tabel)\n`;
    delta += `-- -------------------------------------------------------------\n\n`;

    missingTables.forEach(t => {
      const efModel = EF_CORE_TABLES.find(m => m.name === t.tableName);
      if (!efModel) return;

      if (engine === 'sqlserver') {
        delta += `IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'${t.tableName}' AND schema_id = SCHEMA_ID(N'dbo'))\n`;
        delta += `BEGIN\n    CREATE TABLE [dbo].[${t.tableName}] (\n`;
        const cols = efModel.columns.map(c => `        [${c.name}] ${c.typeByEngine?.sqlserver || c.type} ${c.nullable ? 'NULL' : 'NOT NULL'}`);
        delta += cols.join(',\n') + `\n    );\nEND\nGO\n\n`;
      } else if (engine === 'postgres') {
        delta += `CREATE TABLE IF NOT EXISTS "${t.tableName}" (\n`;
        const cols = efModel.columns.map(c => `    "${c.name}" ${c.typeByEngine?.postgres || c.type} ${c.nullable ? 'NULL' : 'NOT NULL'}`);
        delta += cols.join(',\n') + `\n);\n\n`;
      } else if (engine === 'sqlite') {
        delta += `CREATE TABLE IF NOT EXISTS "${t.tableName}" (\n`;
        const cols = efModel.columns.map(c => `    "${c.name}" ${c.typeByEngine?.sqlite || 'TEXT'} ${c.nullable ? 'NULL' : 'NOT NULL'}`);
        delta += cols.join(',\n') + `\n);\n\n`;
      } else if (engine === 'oracle') {
        delta += `DECLARE v_count NUMBER; BEGIN\n`;
        delta += `    SELECT count(*) INTO v_count FROM user_tables WHERE table_name = '${t.tableName.toUpperCase()}';\n`;
        delta += `    IF v_count = 0 THEN\n`;
        delta += `        EXECUTE IMMEDIATE 'CREATE TABLE "${t.tableName}" (\n`;
        const cols = efModel.columns.map(c => `            "${c.name}" ${c.typeByEngine?.oracle || 'VARCHAR2(255)'} ${c.nullable ? 'NULL' : 'NOT NULL'}`);
        delta += cols.join(',\\n') + `\n        )';\n`;
        delta += `    END IF;\nEND;\n/\n\n`;
      }
    });
  }

  // 2. Brakujące kolumny w istniejących tabelach - ZAWSZE DODAWANE JAKO NULL!
  if (tablesWithMissingCols.length > 0) {
    delta += `-- -------------------------------------------------------------\n`;
    delta += `-- ETAP 2: DODAWANIE BRAKUJĄCYCH KOLUMN JAKO NULL-OWALNE\n`;
    delta += `-- (Zgodnie z wymogiem: jeśli kolumny nie ma to dodaje NULL-owalną)\n`;
    delta += `-- -------------------------------------------------------------\n\n`;

    tablesWithMissingCols.forEach(t => {
      t.missingColumns.forEach(c => {
        if (engine === 'sqlserver') {
          const type = (c.typeByEngine?.sqlserver || c.type).replace(/IDENTITY\(.*?\)/i, '').trim();
          delta += `IF EXISTS (SELECT * FROM sys.tables WHERE name = N'${t.tableName}' AND schema_id = SCHEMA_ID(N'dbo'))\n`;
          delta += `   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[${t.tableName}]') AND name = N'${c.name}')\n`;
          delta += `BEGIN\n`;
          delta += `    ALTER TABLE [dbo].[${t.tableName}] ADD [${c.name}] ${type} NULL;\n`;
          delta += `    PRINT 'Dodano brakującą kolumnę [${c.name}] (NULL) do [${t.tableName}]';\n`;
          delta += `END\nGO\n\n`;
        } else if (engine === 'postgres') {
          const type = (c.typeByEngine?.postgres || c.type).replace(/serial/i, 'integer').trim();
          delta += `DO $$ BEGIN\n`;
          delta += `    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '${t.tableName}' AND column_name = '${c.name}') THEN\n`;
          delta += `        ALTER TABLE "${t.tableName}" ADD COLUMN "${c.name}" ${type} NULL;\n`;
          delta += `        RAISE NOTICE 'Dodano kolumnę "${c.name}" (NULL) do tabeli "${t.tableName}"';\n`;
          delta += `    END IF;\n`;
          delta += `END $$;\n\n`;
        } else if (engine === 'sqlite') {
          const type = c.typeByEngine?.sqlite || 'TEXT';
          delta += `ALTER TABLE "${t.tableName}" ADD COLUMN "${c.name}" ${type} NULL;\n`;
        } else if (engine === 'oracle') {
          const type = c.typeByEngine?.oracle || 'VARCHAR2(255)';
          delta += `DECLARE v_count NUMBER; BEGIN\n`;
          delta += `    SELECT count(*) INTO v_count FROM user_tab_cols WHERE table_name = '${t.tableName.toUpperCase()}' AND column_name = '${c.name.toUpperCase()}';\n`;
          delta += `    IF v_count = 0 THEN\n`;
          delta += `        EXECUTE IMMEDIATE 'ALTER TABLE "${t.tableName}" ADD ("${c.name}" ${type} NULL)';\n`;
          delta += `    END IF;\nEND;\n/\n\n`;
        }
      });
    });
  }

  // 3. Brakujące indeksy
  if (tablesWithMissingIndexes.length > 0) {
    delta += `-- -------------------------------------------------------------\n`;
    delta += `-- ETAP 3: TWORZENIE BRAKUJĄCYCH INDEKSÓW\n`;
    delta += `-- -------------------------------------------------------------\n\n`;

    tablesWithMissingIndexes.forEach(t => {
      t.missingIndexes.forEach(idx => {
        if (engine === 'sqlserver') {
          const cols = idx.columns.map(c => `[${c}] ASC`).join(', ');
          delta += `IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'${idx.name}' AND object_id = OBJECT_ID(N'[dbo].[${t.tableName}]'))\n`;
          delta += `BEGIN\n    CREATE ${idx.isUnique ? 'UNIQUE ' : ''}NONCLUSTERED INDEX [${idx.name}] ON [dbo].[${t.tableName}] (${cols});\nEND\nGO\n\n`;
        } else if (engine === 'postgres' || engine === 'sqlite') {
          const cols = idx.columns.map(c => `"${c}"`).join(', ');
          delta += `CREATE ${idx.isUnique ? 'UNIQUE ' : ''}INDEX IF NOT EXISTS "${idx.name}" ON "${t.tableName}" (${cols});\n`;
        } else if (engine === 'oracle') {
          const cols = idx.columns.map(c => `"${c}"`).join(', ');
          delta += `DECLARE v_count NUMBER; BEGIN\n`;
          delta += `    SELECT count(*) INTO v_count FROM user_indexes WHERE index_name = '${idx.name.toUpperCase()}';\n`;
          delta += `    IF v_count = 0 THEN\n`;
          delta += `        EXECUTE IMMEDIATE 'CREATE ${idx.isUnique ? 'UNIQUE ' : ''}INDEX "${idx.name}" ON "${t.tableName}" (${cols})';\n`;
          delta += `    END IF;\nEND;\n/\n\n`;
        }
      });
    });
  }

  return delta;
}

/**
 * Przygotowanie planu wykonania dla przycisku "Wgraj zmiany" (Apply Changes)
 */
export function buildExecutionSteps(diffResult: SchemaCompareResult): MigrationExecutionStep[] {
  const steps: MigrationExecutionStep[] = [];
  let stepId = 1;

  steps.push({
    id: `step-${stepId++}`,
    timestamp: new Date().toLocaleTimeString(),
    title: 'Rozpoczęcie bezpiecznej transakcji bazodanowej',
    sql: 'BEGIN TRANSACTION;',
    status: 'pending'
  });

  steps.push({
    id: `step-${stepId++}`,
    timestamp: new Date().toLocaleTimeString(),
    title: 'Sprawdzenie spójności metadanych bazy i uprawnień DDL',
    sql: '-- VALIDATE DDL PRIVILEGES & CATALOG LOCKS',
    status: 'pending'
  });

  // Brakujące tabele
  diffResult.tables.filter(t => t.status === 'missing_table').forEach(t => {
    steps.push({
      id: `step-${stepId++}`,
      timestamp: new Date().toLocaleTimeString(),
      title: `Utworzenie brakującej tabeli [${t.tableName}]`,
      sql: `CREATE TABLE IF NOT EXISTS "${t.tableName}" (...)`,
      status: 'pending'
    });
  });

  // Brakujące kolumny (NULL)
  diffResult.tables.filter(t => t.status === 'has_missing_columns').forEach(t => {
    t.missingColumns.forEach(c => {
      steps.push({
        id: `step-${stepId++}`,
        timestamp: new Date().toLocaleTimeString(),
        title: `Dodanie kolumny [${c.name}] (NULL) do tabeli [${t.tableName}]`,
        sql: `ALTER TABLE "${t.tableName}" ADD COLUMN "${c.name}" ${c.type} NULL;`,
        status: 'pending'
      });
    });
  });

  // Brakujące indeksy
  diffResult.tables.forEach(t => {
    t.missingIndexes.forEach(idx => {
      steps.push({
        id: `step-${stepId++}`,
        timestamp: new Date().toLocaleTimeString(),
        title: `Utworzenie indeksu wydajnościowego [${idx.name}]`,
        sql: `CREATE INDEX IF NOT EXISTS "${idx.name}" ON "${t.tableName}" (...)`,
        status: 'pending'
      });
    });
  });

  steps.push({
    id: `step-${stepId++}`,
    timestamp: new Date().toLocaleTimeString(),
    title: 'Aktualizacja historii migracji EF Core (__EFMigrationsHistory)',
    sql: `INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('${new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14)}_SchemaCompareSync', '10.0.0');`,
    status: 'pending'
  });

  steps.push({
    id: `step-${stepId++}`,
    timestamp: new Date().toLocaleTimeString(),
    title: 'Zatwierdzenie transakcji i odświeżenie indeksów (COMMIT)',
    sql: 'COMMIT TRANSACTION;',
    status: 'pending'
  });

  return steps;
}
