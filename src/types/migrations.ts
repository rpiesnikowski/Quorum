export type SqlEngine = 'sqlserver' | 'postgres' | 'sqlite' | 'oracle';

export type OutputFormat = 'sql' | 'liquibase-xml' | 'liquibase-yaml' | 'liquibase-sql';

export interface ColumnDefinition {
  name: string;
  type: string;
  typeByEngine?: {
    sqlserver: string;
    postgres: string;
    sqlite: string;
    oracle: string;
  };
  nullable: boolean;
  isPrimaryKey?: boolean;
  isAutoIncrement?: boolean;
  defaultValue?: string;
  description?: string;
}

export interface IndexDefinition {
  name: string;
  tableName: string;
  columns: string[];
  isUnique?: boolean;
}

export interface ForeignKeyDefinition {
  name: string;
  tableName: string;
  column: string;
  principalTable: string;
  principalColumn: string;
  onDelete?: 'CASCADE' | 'SET NULL' | 'RESTRICT' | 'NO ACTION';
}

export interface TableModel {
  name: string;
  category: 'gateway' | 'identity' | 'openiddict' | 'grants';
  categoryLabel: string;
  description: string;
  columns: ColumnDefinition[];
  indexes: IndexDefinition[];
  foreignKeys: ForeignKeyDefinition[];
}

export interface TableDiff {
  tableName: string;
  category: string;
  status: 'matched' | 'missing_table' | 'has_missing_columns';
  existingColumns: string[];
  missingColumns: ColumnDefinition[]; // Dodawane jako NULL-owalne!
  existingIndexes: string[];
  missingIndexes: IndexDefinition[];
}

export interface SchemaCompareResult {
  engine: SqlEngine;
  connectionString: string;
  databaseName: string;
  timestamp: string;
  tables: TableDiff[];
  summary: {
    totalExpectedTables: number;
    existingTablesCount: number;
    missingTablesCount: number;
    missingColumnsCount: number;
    missingIndexesCount: number;
    synchronizedPercentage: number;
  };
  deltaSql: string;
}

export interface MigrationExecutionStep {
  id: string;
  timestamp: string;
  title: string;
  sql: string;
  status: 'pending' | 'running' | 'success' | 'error';
  durationMs?: number;
  message?: string;
}
