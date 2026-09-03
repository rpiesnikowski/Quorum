import { SqlEngine, OutputFormat, TableModel, ColumnDefinition, IndexDefinition, TableDiff, SchemaCompareResult, MigrationExecutionStep } from '../types/migrations';
import { EF_CORE_TABLES, SOURCE_QUORUM_CONFIG } from '../data/efCoreSchemaData';

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

export const generateDeltaPureSql = generateDeltaMigrationSql;

/**
 * Generowanie skryptu Delta w formacie Liquibase XML (tylko różnice)
 */
export function generateDeltaLiquibaseXml(diffs: TableDiff[]): string {
  const missingTables = diffs.filter(d => d.status === 'missing_table');
  const tablesWithMissingCols = diffs.filter(d => d.status === 'has_missing_columns');
  const tablesWithMissingIndexes = diffs.filter(d => d.missingIndexes.length > 0);

  let xml = `<?xml version="1.0" encoding="UTF-8"?>\n`;
  xml += `<databaseChangeLog\n`;
  xml += `    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"\n`;
  xml += `    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"\n`;
  xml += `    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog\n`;
  xml += `        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-4.20.xsd">\n\n`;
  xml += `    <!-- ========================================================================= -->\n`;
  xml += `    <!-- QUORUM SCHEMA COMPARE - LIQUIBASE DELTA CHANGELOG -->\n`;
  xml += `    <!-- WYGENEROWANO TYLKO WYKRYTE RÓŻNICE (DELTA) Z BAZY ŹRÓDŁOWEJ DO DOCELOWEJ -->\n`;
  xml += `    <!-- ========================================================================= -->\n\n`;

  if (missingTables.length === 0 && tablesWithMissingCols.length === 0 && tablesWithMissingIndexes.length === 0) {
    xml += `    <!-- Baza danych jest w 100% zgodna ze schematem Quorum. Brak obiektów delta. -->\n`;
    xml += `</databaseChangeLog>\n`;
    return xml;
  }

  // 1. Brakujące tabele
  missingTables.forEach(t => {
    const efModel = EF_CORE_TABLES.find(m => m.name === t.tableName);
    if (!efModel) return;

    xml += `    <!-- Brakująca tabela: ${t.tableName} -->\n`;
    xml += `    <changeSet id="delta-create-table-${t.tableName.toLowerCase()}" author="quorum-admin">\n`;
    xml += `        <preConditions onFail="MARK_RAN">\n`;
    xml += `            <not>\n`;
    xml += `                <tableExists tableName="${t.tableName}" />\n`;
    xml += `            </not>\n`;
    xml += `        </preConditions>\n`;
    xml += `        <createTable tableName="${t.tableName}" remarks="Wygenerowano automatycznie przez Quorum Schema Compare">\n`;
    efModel.columns.forEach(col => {
      const type = col.type.includes('varchar') ? 'varchar(255)' : col.type;
      xml += `            <column name="${col.name}" type="${type}">\n`;
      if (col.isPrimaryKey) {
        xml += `                <constraints primaryKey="true" nullable="false" />\n`;
      } else if (!col.nullable) {
        xml += `                <constraints nullable="false" />\n`;
      } else {
        xml += `                <constraints nullable="true" />\n`;
      }
      xml += `            </column>\n`;
    });
    xml += `        </createTable>\n`;
    xml += `    </changeSet>\n\n`;
  });

  // 2. Brakujące kolumny (dodawane jako NULL!)
  tablesWithMissingCols.forEach(t => {
    t.missingColumns.forEach(c => {
      xml += `    <!-- Brakująca kolumna (NULL): ${c.name} w tabeli ${t.tableName} -->\n`;
      xml += `    <changeSet id="delta-add-col-${t.tableName.toLowerCase()}-${c.name.toLowerCase()}" author="quorum-admin">\n`;
      xml += `        <preConditions onFail="MARK_RAN">\n`;
      xml += `            <tableExists tableName="${t.tableName}" />\n`;
      xml += `            <not>\n`;
      xml += `                <columnExists tableName="${t.tableName}" columnName="${c.name}" />\n`;
      xml += `            </not>\n`;
      xml += `        </preConditions>\n`;
      xml += `        <addColumn tableName="${t.tableName}">\n`;
      xml += `            <column name="${c.name}" type="${c.type}">\n`;
      xml += `                <constraints nullable="true" />\n`;
      xml += `            </column>\n`;
      xml += `        </addColumn>\n`;
      xml += `    </changeSet>\n\n`;
    });
  });

  // 3. Brakujące indeksy
  tablesWithMissingIndexes.forEach(t => {
    t.missingIndexes.forEach(idx => {
      xml += `    <!-- Brakujący indeks: ${idx.name} -->\n`;
      xml += `    <changeSet id="delta-create-idx-${idx.name.toLowerCase()}" author="quorum-admin">\n`;
      xml += `        <preConditions onFail="MARK_RAN">\n`;
      xml += `            <tableExists tableName="${t.tableName}" />\n`;
      xml += `        </preConditions>\n`;
      xml += `        <createIndex indexName="${idx.name}" tableName="${t.tableName}" unique="${idx.isUnique ? 'true' : 'false'}">\n`;
      idx.columns.forEach(col => {
        xml += `            <column name="${col}" />\n`;
      });
      xml += `        </createIndex>\n`;
      xml += `    </changeSet>\n\n`;
    });
  });

  xml += `</databaseChangeLog>\n`;
  return xml;
}

/**
 * Generowanie skryptu Delta w formacie Liquibase YAML (tylko różnice)
 */
export function generateDeltaLiquibaseYaml(diffs: TableDiff[]): string {
  const missingTables = diffs.filter(d => d.status === 'missing_table');
  const tablesWithMissingCols = diffs.filter(d => d.status === 'has_missing_columns');
  const tablesWithMissingIndexes = diffs.filter(d => d.missingIndexes.length > 0);

  let yaml = `databaseChangeLog:\n`;
  yaml += `  # =========================================================================\n`;
  yaml += `  # QUORUM SCHEMA COMPARE - LIQUIBASE DELTA CHANGELOG (YAML)\n`;
  yaml += `  # TYLKO BRAKUJĄCE TABELE, KOLUMNY I INDEKSY\n`;
  yaml += `  # =========================================================================\n\n`;

  if (missingTables.length === 0 && tablesWithMissingCols.length === 0 && tablesWithMissingIndexes.length === 0) {
    yaml += `  # Schemat bazy danych jest w 100% zgodny. Brak zmian delta.\n`;
    return yaml;
  }

  // 1. Brakujące tabele
  missingTables.forEach(t => {
    const efModel = EF_CORE_TABLES.find(m => m.name === t.tableName);
    if (!efModel) return;

    yaml += `  - changeSet:\n`;
    yaml += `      id: delta-create-table-${t.tableName.toLowerCase()}\n`;
    yaml += `      author: quorum-admin\n`;
    yaml += `      preConditions:\n`;
    yaml += `        - onFail: MARK_RAN\n`;
    yaml += `        - not:\n`;
    yaml += `            - tableExists:\n`;
    yaml += `                tableName: ${t.tableName}\n`;
    yaml += `      changes:\n`;
    yaml += `        - createTable:\n`;
    yaml += `            tableName: ${t.tableName}\n`;
    yaml += `            columns:\n`;
    efModel.columns.forEach(col => {
      yaml += `              - column:\n`;
      yaml += `                  name: ${col.name}\n`;
      yaml += `                  type: ${col.type.includes('varchar') ? 'varchar(255)' : col.type}\n`;
      if (col.isPrimaryKey) {
        yaml += `                  constraints:\n                    primaryKey: true\n                    nullable: false\n`;
      } else if (!col.nullable) {
        yaml += `                  constraints:\n                    nullable: false\n`;
      } else {
        yaml += `                  constraints:\n                    nullable: true\n`;
      }
    });
  });

  // 2. Brakujące kolumny (NULL)
  tablesWithMissingCols.forEach(t => {
    t.missingColumns.forEach(c => {
      yaml += `  - changeSet:\n`;
      yaml += `      id: delta-add-col-${t.tableName.toLowerCase()}-${c.name.toLowerCase()}\n`;
      yaml += `      author: quorum-admin\n`;
      yaml += `      preConditions:\n`;
      yaml += `        - onFail: MARK_RAN\n`;
      yaml += `        - tableExists:\n`;
      yaml += `            tableName: ${t.tableName}\n`;
      yaml += `        - not:\n`;
      yaml += `            - columnExists:\n`;
      yaml += `                tableName: ${t.tableName}\n`;
      yaml += `                columnName: ${c.name}\n`;
      yaml += `      changes:\n`;
      yaml += `        - addColumn:\n`;
      yaml += `            tableName: ${t.tableName}\n`;
      yaml += `            columns:\n`;
      yaml += `              - column:\n`;
      yaml += `                  name: ${c.name}\n`;
      yaml += `                  type: ${c.type}\n`;
      yaml += `                  constraints:\n                    nullable: true\n`;
    });
  });

  // 3. Brakujące indeksy
  tablesWithMissingIndexes.forEach(t => {
    t.missingIndexes.forEach(idx => {
      yaml += `  - changeSet:\n`;
      yaml += `      id: delta-create-idx-${idx.name.toLowerCase()}\n`;
      yaml += `      author: quorum-admin\n`;
      yaml += `      preConditions:\n`;
      yaml += `        - onFail: MARK_RAN\n`;
      yaml += `        - tableExists:\n`;
      yaml += `            tableName: ${t.tableName}\n`;
      yaml += `      changes:\n`;
      yaml += `        - createIndex:\n`;
      yaml += `            indexName: ${idx.name}\n`;
      yaml += `            tableName: ${t.tableName}\n`;
      yaml += `            unique: ${idx.isUnique ? 'true' : 'false'}\n`;
      yaml += `            columns:\n`;
      idx.columns.forEach(col => {
        yaml += `              - column:\n                  name: ${col}\n`;
      });
    });
  });

  return yaml;
}

/**
 * Generowanie skryptu Delta w formacie Liquibase Formatted SQL (tylko różnice)
 */
export function generateDeltaLiquibaseFormattedSql(diffs: TableDiff[], engine: SqlEngine): string {
  let res = `--liquibase formatted sql\n\n`;
  res += `-- =========================================================================\n`;
  res += `-- QUORUM SCHEMA COMPARE - LIQUIBASE FORMATTED SQL DELTA\n`;
  res += `-- DBMS: ${engine}\n`;
  res += `-- =========================================================================\n\n`;

  const missingTables = diffs.filter(d => d.status === 'missing_table');
  const tablesWithMissingCols = diffs.filter(d => d.status === 'has_missing_columns');
  const tablesWithMissingIndexes = diffs.filter(d => d.missingIndexes.length > 0);

  if (missingTables.length === 0 && tablesWithMissingCols.length === 0 && tablesWithMissingIndexes.length === 0) {
    res += `-- Baza danych jest w 100% zgodna ze schematem Quorum. Brak obiektów delta.\n`;
    return res;
  }

  missingTables.forEach(t => {
    const efModel = EF_CORE_TABLES.find(m => m.name === t.tableName);
    if (!efModel) return;

    res += `--changeset quorum-admin:delta-create-table-${t.tableName.toLowerCase()} dbms:${engine}\n`;
    res += `--preconditions onFail:MARK_RAN\n`;
    if (engine === 'postgres') {
      res += `CREATE TABLE IF NOT EXISTS "${t.tableName}" (\n`;
      res += efModel.columns.map(c => `    "${c.name}" ${c.typeByEngine?.postgres || c.type} ${c.nullable ? 'NULL' : 'NOT NULL'}`).join(',\n') + '\n);\n\n';
    } else if (engine === 'sqlserver') {
      res += `IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'${t.tableName}')\n`;
      res += `CREATE TABLE [dbo].[${t.tableName}] (\n`;
      res += efModel.columns.map(c => `    [${c.name}] ${c.typeByEngine?.sqlserver || c.type} ${c.nullable ? 'NULL' : 'NOT NULL'}`).join(',\n') + '\n);\nGO\n\n';
    } else if (engine === 'sqlite') {
      res += `CREATE TABLE IF NOT EXISTS "${t.tableName}" (\n`;
      res += efModel.columns.map(c => `    "${c.name}" ${c.typeByEngine?.sqlite || 'TEXT'} ${c.nullable ? 'NULL' : 'NOT NULL'}`).join(',\n') + '\n);\n\n';
    } else {
      res += `CREATE TABLE "${t.tableName}" (\n`;
      res += efModel.columns.map(c => `    "${c.name}" ${c.typeByEngine?.oracle || 'VARCHAR2(255)'} ${c.nullable ? 'NULL' : 'NOT NULL'}`).join(',\n') + '\n);\n\n';
    }
  });

  tablesWithMissingCols.forEach(t => {
    t.missingColumns.forEach(c => {
      res += `--changeset quorum-admin:delta-add-col-${t.tableName.toLowerCase()}-${c.name.toLowerCase()} dbms:${engine}\n`;
      if (engine === 'postgres') {
        res += `ALTER TABLE "${t.tableName}" ADD COLUMN IF NOT EXISTS "${c.name}" ${c.typeByEngine?.postgres || c.type} NULL;\n\n`;
      } else if (engine === 'sqlserver') {
        res += `IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[${t.tableName}]') AND name = N'${c.name}')\n`;
        res += `ALTER TABLE [dbo].[${t.tableName}] ADD [${c.name}] ${c.typeByEngine?.sqlserver || c.type} NULL;\nGO\n\n`;
      } else if (engine === 'sqlite') {
        res += `ALTER TABLE "${t.tableName}" ADD COLUMN "${c.name}" ${c.typeByEngine?.sqlite || 'TEXT'} NULL;\n\n`;
      } else {
        res += `ALTER TABLE "${t.tableName}" ADD ("${c.name}" ${c.typeByEngine?.oracle || 'VARCHAR2(255)'} NULL);\n\n`;
      }
    });
  });

  return res;
}

/**
 * Główna uniwersalna funkcja generująca skrypt Delta w wybranym formacie wyjściowym
 */
export function generateDeltaScript(diffs: TableDiff[], engine: SqlEngine, format: OutputFormat): string {
  switch (format) {
    case 'sql':
      return generateDeltaPureSql(diffs, engine);
    case 'liquibase-xml':
      return generateDeltaLiquibaseXml(diffs);
    case 'liquibase-yaml':
      return generateDeltaLiquibaseYaml(diffs);
    case 'liquibase-sql':
      return generateDeltaLiquibaseFormattedSql(diffs, engine);
    default:
      return generateDeltaPureSql(diffs, engine);
  }
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
