import { SqlEngine, OutputFormat, TableModel, ColumnDefinition } from '../types/migrations';

/**
 * Zwraca właściwy typ danych dla danego silnika bazodanowego.
 */
function getDataType(col: ColumnDefinition, engine: SqlEngine): string {
  if (col.typeByEngine && col.typeByEngine[engine]) {
    return col.typeByEngine[engine];
  }
  switch (engine) {
    case 'sqlserver':
      if (col.type.includes('varchar')) return col.type.replace('varchar', 'nvarchar');
      if (col.type === 'integer') return 'int';
      if (col.type === 'boolean') return 'bit';
      if (col.type === 'timestamp') return 'datetime2';
      return col.type;
    case 'postgres':
      if (col.type.includes('nvarchar')) return col.type.replace('nvarchar', 'varchar');
      if (col.type === 'boolean') return 'boolean';
      if (col.type === 'timestamp') return 'timestamp with time zone';
      return col.type;
    case 'sqlite':
      if (col.type.includes('char') || col.type.includes('text')) return 'TEXT';
      if (col.type === 'boolean' || col.type.includes('int')) return 'INTEGER';
      if (col.type === 'timestamp') return 'TEXT';
      return 'TEXT';
    case 'oracle':
      if (col.type.includes('varchar')) return col.type.replace(/n?varchar/i, 'VARCHAR2');
      if (col.type === 'integer') return 'NUMBER(10)';
      if (col.type === 'boolean') return 'NUMBER(1)';
      if (col.type === 'timestamp') return 'TIMESTAMP WITH TIME ZONE';
      return col.type;
    default:
      return col.type;
  }
}

/**
 * Generator czystego, idempotentnego kodu SQL dla wybranego silnika.
 */
export function generatePureIdempotentSql(tables: TableModel[], engine: SqlEngine): string {
  const headerComment = `-- =========================================================================
-- QUORUM IDENTITYSERVER & API GATEWAY - IDEMPOTENTNA MIGRACJA SCHEMATU EF
-- SILNIK DOCELOWY: ${engine.toUpperCase()}
-- CHARAKTERYSTYKA: 100% IDEMPOTENTNA (BEZPIECZNA PRZY WIELOKROTNYM URUCHOMIENIU)
-- ZASADY:
-- 1. Nie usuwa istniejących tabel (IF NOT EXISTS)
-- 2. Nie duplikuje istniejących kolumn (Sprawdzenie katalogu systemowego)
-- 3. Brakujące kolumny dodawane jako NULL-owalne (ALTER TABLE ... ADD ... NULL)
-- 4. Indeksy oraz klucze obce weryfikowane przed utworzeniem
-- =========================================================================\n\n`;

  let sql = headerComment;

  switch (engine) {
    case 'sqlserver':
      sql += generateSqlServerScript(tables);
      break;
    case 'postgres':
      sql += generatePostgreSqlScript(tables);
      break;
    case 'sqlite':
      sql += generateSqliteScript(tables);
      break;
    case 'oracle':
      sql += generateOracleScript(tables);
      break;
  }

  return sql;
}

// -------------------------------------------------------------
// 1. SQL SERVER (T-SQL) - Idempotentny
// -------------------------------------------------------------
function generateSqlServerScript(tables: TableModel[]): string {
  let res = `SET NOCOUNT ON;\nGO\n\n`;

  tables.forEach(table => {
    res += `-- -------------------------------------------------------------\n`;
    res += `-- Tabela: [dbo].[${table.name}] (${table.categoryLabel})\n`;
    res += `-- -------------------------------------------------------------\n`;

    // 1. Sprawdzenie i utworzenie tabeli
    res += `IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'${table.name}' AND schema_id = SCHEMA_ID(N'dbo'))\n`;
    res += `BEGIN\n`;
    res += `    PRINT 'Tworzenie brakującej tabeli [dbo].[${table.name}]...';\n`;
    res += `    CREATE TABLE [dbo].[${table.name}] (\n`;

    const colDefs = table.columns.map(c => {
      const type = getDataType(c, 'sqlserver');
      const nullability = c.nullable ? 'NULL' : 'NOT NULL';
      const def = c.defaultValue ? ` DEFAULT ${c.defaultValue}` : '';
      return `        [${c.name}] ${type} ${nullability}${def}`;
    });

    const pkCol = table.columns.find(c => c.isPrimaryKey);
    if (pkCol) {
      colDefs.push(`        CONSTRAINT [PK_${table.name}] PRIMARY KEY CLUSTERED ([${pkCol.name}] ASC)`);
    }

    res += colDefs.join(',\n') + '\n';
    res += `    );\n`;
    res += `    PRINT 'Tabela [dbo].[${table.name}] została pomyślnie utworzona.';\n`;
    res += `END\n`;
    res += `ELSE\n`;
    res += `BEGIN\n`;
    res += `    PRINT 'Tabela [dbo].[${table.name}] już istnieje - sprawdzanie brakujących kolumn...';\n`;
    res += `END\nGO\n\n`;

    // 2. Idempotentne dodawanie brakujących kolumn (jako NULL-owalne!)
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const type = getDataType(c, 'sqlserver').replace(/IDENTITY\(.*?\)/i, '').trim();
      res += `IF EXISTS (SELECT * FROM sys.tables WHERE name = N'${table.name}' AND schema_id = SCHEMA_ID(N'dbo'))\n`;
      res += `   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[${table.name}]') AND name = N'${c.name}')\n`;
      res += `BEGIN\n`;
      res += `    PRINT 'Dodawanie brakującej kolumny [${c.name}] (NULL) do tabeli [dbo].[${table.name}]...';\n`;
      res += `    ALTER TABLE [dbo].[${table.name}] ADD [${c.name}] ${type} NULL;\n`;
      res += `END\nGO\n`;
    });
    res += `\n`;

    // 3. Indeksy
    table.indexes.forEach(idx => {
      const cols = idx.columns.map(c => `[${c}] ASC`).join(', ');
      const unique = idx.isUnique ? 'UNIQUE ' : '';
      res += `IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'${idx.name}' AND object_id = OBJECT_ID(N'[dbo].[${table.name}]'))\n`;
      res += `BEGIN\n`;
      res += `    PRINT 'Tworzenie brakującego indeksu [${idx.name}]...';\n`;
      res += `    CREATE ${unique}NONCLUSTERED INDEX [${idx.name}] ON [dbo].[${table.name}] (${cols});\n`;
      res += `END\nGO\n`;
    });
    res += `\n`;

    // 4. Klucze obce
    table.foreignKeys.forEach(fk => {
      res += `IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'${fk.name}' AND parent_object_id = OBJECT_ID(N'[dbo].[${table.name}]'))\n`;
      res += `BEGIN\n`;
      res += `    PRINT 'Tworzenie powiązania relacyjnego (FK) [${fk.name}]...';\n`;
      res += `    ALTER TABLE [dbo].[${table.name}] WITH CHECK ADD CONSTRAINT [${fk.name}] FOREIGN KEY([${fk.column}])\n`;
      res += `    REFERENCES [dbo].[${fk.principalTable}] ([${fk.principalColumn}])`;
      if (fk.onDelete) {
        res += ` ON DELETE ${fk.onDelete}`;
      }
      res += `;\n`;
      res += `    ALTER TABLE [dbo].[${table.name}] CHECK CONSTRAINT [${fk.name}];\n`;
      res += `END\nGO\n`;
    });

    res += `\n`;
  });

  return res;
}

// -------------------------------------------------------------
// 2. POSTGRESQL (PL/pgSQL) - Idempotentny
// -------------------------------------------------------------
function generatePostgreSqlScript(tables: TableModel[]): string {
  let res = ``;

  tables.forEach(table => {
    res += `-- -------------------------------------------------------------\n`;
    res += `-- Tabela: "${table.name}" (${table.categoryLabel})\n`;
    res += `-- -------------------------------------------------------------\n`;

    // 1. Tworzenie tabeli IF NOT EXISTS
    res += `CREATE TABLE IF NOT EXISTS "${table.name}" (\n`;
    const colDefs = table.columns.map(c => {
      const type = getDataType(c, 'postgres');
      if (c.isPrimaryKey) {
        return `    "${c.name}" ${type} PRIMARY KEY`;
      }
      const nullability = c.nullable ? 'NULL' : 'NOT NULL';
      const def = c.defaultValue ? ` DEFAULT ${c.defaultValue}` : '';
      return `    "${c.name}" ${type} ${nullability}${def}`;
    });
    res += colDefs.join(',\n') + '\n);\n\n';

    // 2. Idempotentne dodawanie brakujących kolumn (NULL-owalne!)
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const type = getDataType(c, 'postgres').replace(/serial/i, 'integer').trim();
      res += `DO $$ BEGIN\n`;
      res += `    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '${table.name}' AND column_name = '${c.name}') THEN\n`;
      res += `        ALTER TABLE "${table.name}" ADD COLUMN "${c.name}" ${type} NULL;\n`;
      res += `        RAISE NOTICE 'Dodano brakującą kolumnę "${c.name}" (NULL) do tabeli "${table.name}"';\n`;
      res += `    END IF;\n`;
      res += `END $$;\n`;
    });
    res += `\n`;

    // 3. Indeksy IF NOT EXISTS
    table.indexes.forEach(idx => {
      const cols = idx.columns.map(c => `"${c}"`).join(', ');
      const unique = idx.isUnique ? 'UNIQUE ' : '';
      res += `CREATE ${unique}INDEX IF NOT EXISTS "${idx.name}" ON "${table.name}" (${cols});\n`;
    });
    res += `\n`;

    // 4. Klucze obce idempotentnie
    table.foreignKeys.forEach(fk => {
      res += `DO $$ BEGIN\n`;
      res += `    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = '${fk.name}') THEN\n`;
      res += `        ALTER TABLE "${table.name}" ADD CONSTRAINT "${fk.name}" FOREIGN KEY ("${fk.column}")\n`;
      res += `        REFERENCES "${fk.principalTable}" ("${fk.principalColumn}")`;
      if (fk.onDelete) {
        res += ` ON DELETE ${fk.onDelete}`;
      }
      res += `;\n`;
      res += `        RAISE NOTICE 'Utworzono klucz obcy "${fk.name}"';\n`;
      res += `    END IF;\n`;
      res += `END $$;\n`;
    });

    res += `\n`;
  });

  return res;
}

// -------------------------------------------------------------
// 3. SQLITE - Idempotentny
// -------------------------------------------------------------
function generateSqliteScript(tables: TableModel[]): string {
  let res = `PRAGMA foreign_keys = ON;\n\n`;

  tables.forEach(table => {
    res += `-- -------------------------------------------------------------\n`;
    res += `-- Tabela: "${table.name}" (${table.categoryLabel})\n`;
    res += `-- -------------------------------------------------------------\n`;

    // 1. Tabela IF NOT EXISTS
    res += `CREATE TABLE IF NOT EXISTS "${table.name}" (\n`;
    const colDefs = table.columns.map(c => {
      const type = getDataType(c, 'sqlite');
      if (c.isPrimaryKey && c.isAutoIncrement) {
        return `    "${c.name}" INTEGER PRIMARY KEY AUTOINCREMENT`;
      }
      if (c.isPrimaryKey) {
        return `    "${c.name}" TEXT PRIMARY KEY`;
      }
      const nullability = c.nullable ? 'NULL' : 'NOT NULL';
      const def = c.defaultValue ? ` DEFAULT ${c.defaultValue}` : '';
      return `    "${c.name}" ${type} ${nullability}${def}`;
    });
    res += colDefs.join(',\n') + '\n);\n\n';

    // 2. Kolumny NULL-owalne w SQLite (skrypt ALTER TABLE ADD COLUMN NULL)
    res += `-- Skrypty aktualizacji schematu (dla istniejącej bazy SQLite - dodanie kolumn z opcją NULL):\n`;
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const type = getDataType(c, 'sqlite').replace(/INTEGER PRIMARY KEY AUTOINCREMENT/i, 'INTEGER').trim();
      res += `-- ALTER TABLE "${table.name}" ADD COLUMN "${c.name}" ${type} NULL;\n`;
    });
    res += `\n`;

    // 3. Indeksy IF NOT EXISTS
    table.indexes.forEach(idx => {
      const cols = idx.columns.map(c => `"${c}"`).join(', ');
      const unique = idx.isUnique ? 'UNIQUE ' : '';
      res += `CREATE ${unique}INDEX IF NOT EXISTS "${idx.name}" ON "${table.name}" (${cols});\n`;
    });

    res += `\n`;
  });

  return res;
}

// -------------------------------------------------------------
// 4. ORACLE (PL/SQL) - Idempotentny
// -------------------------------------------------------------
function generateOracleScript(tables: TableModel[]): string {
  let res = `SET SERVEROUTPUT ON;\n\n`;

  tables.forEach(table => {
    const tableNameUpper = table.name.toUpperCase();
    res += `-- -------------------------------------------------------------\n`;
    res += `-- Tabela: ${tableNameUpper} (${table.categoryLabel})\n`;
    res += `-- -------------------------------------------------------------\n`;

    // 1. Tworzenie tabeli
    res += `DECLARE\n`;
    res += `    v_count NUMBER;\n`;
    res += `BEGIN\n`;
    res += `    SELECT count(*) INTO v_count FROM user_tables WHERE table_name = '${tableNameUpper}';\n`;
    res += `    IF v_count = 0 THEN\n`;
    res += `        DBMS_OUTPUT.PUT_LINE('Tworzenie brakującej tabeli ${tableNameUpper}...');\n`;
    res += `        EXECUTE IMMEDIATE 'CREATE TABLE "${table.name}" (\n`;

    const colDefs = table.columns.map(c => {
      const type = getDataType(c, 'oracle');
      const nullability = c.nullable ? 'NULL' : 'NOT NULL';
      const def = c.defaultValue ? ` DEFAULT ${c.defaultValue}` : '';
      return `            "${c.name}" ${type} ${nullability}${def}`;
    });
    const pkCol = table.columns.find(c => c.isPrimaryKey);
    if (pkCol) {
      colDefs.push(`            CONSTRAINT "PK_${table.name}" PRIMARY KEY ("${pkCol.name}")`);
    }

    res += colDefs.join(',\\n') + `\n        )';\n`;
    res += `        DBMS_OUTPUT.PUT_LINE('Tabela ${tableNameUpper} utworzona pomyślnie.');\n`;
    res += `    ELSE\n`;
    res += `        DBMS_OUTPUT.PUT_LINE('Tabela ${tableNameUpper} już istnieje.');\n`;
    res += `    END IF;\n`;
    res += `END;\n/\n\n`;

    // 2. Idempotentne dodawanie kolumn (NULL-owalne!)
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const colUpper = c.name.toUpperCase();
      const type = getDataType(c, 'oracle');
      res += `DECLARE\n`;
      res += `    v_count NUMBER;\n`;
      res += `BEGIN\n`;
      res += `    SELECT count(*) INTO v_count FROM user_tab_cols WHERE table_name = '${tableNameUpper}' AND column_name = '${colUpper}';\n`;
      res += `    IF v_count = 0 THEN\n`;
      res += `        DBMS_OUTPUT.PUT_LINE('Dodawanie brakującej kolumny "${c.name}" (NULL) do ${tableNameUpper}...');\n`;
      res += `        EXECUTE IMMEDIATE 'ALTER TABLE "${table.name}" ADD ("${c.name}" ${type} NULL)';\n`;
      res += `    END IF;\n`;
      res += `END;\n/\n`;
    });
    res += `\n`;

    // 3. Indeksy
    table.indexes.forEach(idx => {
      const idxUpper = idx.name.toUpperCase();
      const cols = idx.columns.map(c => `"${c}"`).join(', ');
      const unique = idx.isUnique ? 'UNIQUE ' : '';
      res += `DECLARE\n`;
      res += `    v_count NUMBER;\n`;
      res += `BEGIN\n`;
      res += `    SELECT count(*) INTO v_count FROM user_indexes WHERE index_name = '${idxUpper}';\n`;
      res += `    IF v_count = 0 THEN\n`;
      res += `        DBMS_OUTPUT.PUT_LINE('Tworzenie indeksu ${idxUpper}...');\n`;
      res += `        EXECUTE IMMEDIATE 'CREATE ${unique}INDEX "${idx.name}" ON "${table.name}" (${cols})';\n`;
      res += `    END IF;\n`;
      res += `END;\n/\n`;
    });

    res += `\n`;
  });

  return res;
}

// -------------------------------------------------------------
// 5. LIQUIBASE GENERATORS (XML, YAML, FORMATTED SQL)
// -------------------------------------------------------------
export function generateLiquibaseXml(tables: TableModel[]): string {
  let xml = `<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
    http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-4.25.xsd">

    <!-- ================================================================= -->
    <!-- QUORUM IDENTITYSERVER & GATEWAY - LIQUIBASE IDEMPOTENT CHANGELOG  -->
    <!-- ZASADY: preConditions onFail="MARK_RAN" zapewnia idempotentność   -->
    <!-- Dodatkowe kolumny dodawane jako NULL-owalne                       -->
    <!-- ================================================================= -->\n\n`;

  let changeSetCounter = 1;

  tables.forEach(table => {
    const padNum = String(changeSetCounter++).padStart(3, '0');

    // 1. Create table changeSet
    xml += `    <!-- Tworzenie tabeli ${table.name} jeśli nie istnieje -->\n`;
    xml += `    <changeSet id="${padNum}-create-table-${table.name}" author="quorum-ef-migration">\n`;
    xml += `        <preConditions onFail="MARK_RAN">\n`;
    xml += `            <not><tableExists tableName="${table.name}"/></not>\n`;
    xml += `        </preConditions>\n`;
    xml += `        <createTable tableName="${table.name}">\n`;

    table.columns.forEach(c => {
      const type = c.type;
      const isPk = c.isPrimaryKey ? ' primaryKey="true"' : '';
      const nullable = c.nullable ? ' nullable="true"' : ' nullable="false"';
      const autoInc = c.isAutoIncrement ? ' autoIncrement="true"' : '';
      const defVal = c.defaultValue ? ` defaultValue="${c.defaultValue.replace(/'/g, '')}"` : '';

      xml += `            <column name="${c.name}" type="${type}"${autoInc}${defVal}>\n`;
      xml += `                <constraints${isPk}${nullable}/>\n`;
      xml += `            </column>\n`;
    });

    xml += `        </createTable>\n`;
    xml += `    </changeSet>\n\n`;

    // 2. Idempotent column check for existing databases - add missing columns as NULL
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const colNum = String(changeSetCounter++).padStart(3, '0');
      xml += `    <!-- Idempotentne dodanie kolumny ${c.name} jako NULL jeśli tabela istnieje, a kolumny brak -->\n`;
      xml += `    <changeSet id="${colNum}-add-col-${table.name}-${c.name}" author="quorum-ef-migration">\n`;
      xml += `        <preConditions onFail="MARK_RAN">\n`;
      xml += `            <tableExists tableName="${table.name}"/>\n`;
      xml += `            <not><columnExists tableName="${table.name}" columnName="${c.name}"/></not>\n`;
      xml += `        </preConditions>\n`;
      xml += `        <addColumn tableName="${table.name}">\n`;
      xml += `            <column name="${c.name}" type="${c.type}">\n`;
      xml += `                <constraints nullable="true"/>\n`;
      xml += `            </column>\n`;
      xml += `        </addColumn>\n`;
      xml += `    </changeSet>\n\n`;
    });

    // 3. Indeksy
    table.indexes.forEach(idx => {
      const idxNum = String(changeSetCounter++).padStart(3, '0');
      xml += `    <changeSet id="${idxNum}-create-index-${idx.name}" author="quorum-ef-migration">\n`;
      xml += `        <preConditions onFail="MARK_RAN">\n`;
      xml += `            <tableExists tableName="${table.name}"/>\n`;
      xml += `            <not><indexExists indexName="${idx.name}" tableName="${table.name}"/></not>\n`;
      xml += `        </preConditions>\n`;
      xml += `        <createIndex indexName="${idx.name}" tableName="${table.name}" unique="${idx.isUnique ? 'true' : 'false'}">\n`;
      idx.columns.forEach(col => {
        xml += `            <column name="${col}"/>\n`;
      });
      xml += `        </createIndex>\n`;
      xml += `    </changeSet>\n\n`;
    });

    // 4. Klucze obce
    table.foreignKeys.forEach(fk => {
      const fkNum = String(changeSetCounter++).padStart(3, '0');
      xml += `    <changeSet id="${fkNum}-add-fk-${fk.name}" author="quorum-ef-migration">\n`;
      xml += `        <preConditions onFail="MARK_RAN">\n`;
      xml += `            <tableExists tableName="${table.name}"/>\n`;
      xml += `            <tableExists tableName="${fk.principalTable}"/>\n`;
      xml += `            <not><foreignKeyConstraintExists foreignKeyName="${fk.name}"/></not>\n`;
      xml += `        </preConditions>\n`;
      xml += `        <addForeignKeyConstraint baseTableName="${table.name}" baseColumnNames="${fk.column}" constraintName="${fk.name}" referencedTableName="${fk.principalTable}" referencedColumnNames="${fk.principalColumn}" onDelete="${fk.onDelete || 'CASCADE'}"/>\n`;
      xml += `    </changeSet>\n\n`;
    });
  });

  xml += `</databaseChangeLog>\n`;
  return xml;
}

export function generateLiquibaseYaml(tables: TableModel[]): string {
  let yaml = `# =================================================================
# QUORUM IDENTITYSERVER & GATEWAY - LIQUIBASE YAML CHANGELOG
# ZASADY: preConditions onFail: MARK_RAN zapewnia pełną idempotentność
# Brakujące kolumny dodawane jako NULL-owalne
# =================================================================
databaseChangeLog:\n`;

  let changeSetCounter = 1;

  tables.forEach(table => {
    const padNum = String(changeSetCounter++).padStart(3, '0');

    // Tabela
    yaml += `  - changeSet:\n`;
    yaml += `      id: ${padNum}-create-table-${table.name}\n`;
    yaml += `      author: quorum-ef-migration\n`;
    yaml += `      preConditions:\n`;
    yaml += `        - onFail: MARK_RAN\n`;
    yaml += `          not:\n`;
    yaml += `            tableExists:\n`;
    yaml += `              tableName: ${table.name}\n`;
    yaml += `      changes:\n`;
    yaml += `        - createTable:\n`;
    yaml += `            tableName: ${table.name}\n`;
    yaml += `            columns:\n`;
    table.columns.forEach(c => {
      yaml += `              - column:\n`;
      yaml += `                  name: ${c.name}\n`;
      yaml += `                  type: ${c.type}\n`;
      if (c.isAutoIncrement) yaml += `                  autoIncrement: true\n`;
      yaml += `                  constraints:\n`;
      if (c.isPrimaryKey) yaml += `                    primaryKey: true\n`;
      yaml += `                    nullable: ${c.nullable}\n`;
    });

    // Kolumny NULL-owalne
    table.columns.filter(c => !c.isPrimaryKey).forEach(c => {
      const colNum = String(changeSetCounter++).padStart(3, '0');
      yaml += `  - changeSet:\n`;
      yaml += `      id: ${colNum}-add-column-${table.name}-${c.name}\n`;
      yaml += `      author: quorum-ef-migration\n`;
      yaml += `      preConditions:\n`;
      yaml += `        - onFail: MARK_RAN\n`;
      yaml += `          tableExists:\n`;
      yaml += `            tableName: ${table.name}\n`;
      yaml += `          not:\n`;
      yaml += `            columnExists:\n`;
      yaml += `              tableName: ${table.name}\n`;
      yaml += `              columnName: ${c.name}\n`;
      yaml += `      changes:\n`;
      yaml += `        - addColumn:\n`;
      yaml += `            tableName: ${table.name}\n`;
      yaml += `            columns:\n`;
      yaml += `              - column:\n`;
      yaml += `                  name: ${c.name}\n`;
      yaml += `                  type: ${c.type}\n`;
      yaml += `                  constraints:\n`;
      yaml += `                    nullable: true\n`;
    });
  });

  return yaml;
}

export function generateLiquibaseFormattedSql(tables: TableModel[], engine: SqlEngine): string {
  let sql = `-- liquibase formatted sql\n\n`;
  let changeSetCounter = 1;

  tables.forEach(table => {
    const padNum = String(changeSetCounter++).padStart(3, '0');
    sql += `-- changeset quorum-ef-migration:${padNum}-create-${table.name} context:schema dbms:${engine}\n`;
    sql += `-- preconditions onFail:MARK_RAN\n`;
    sql += `-- precondition-table-not-exists tableName:${table.name}\n`;

    const singleTableSql = generatePureIdempotentSql([table], engine);
    sql += singleTableSql + `\n\n`;
  });

  return sql;
}
