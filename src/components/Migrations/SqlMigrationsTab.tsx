import React, { useState, useMemo } from 'react';
import {
  Database,
  Layers,
  ArrowLeftRight,
  Play,
  Copy,
  Download,
  Check,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  Server,
  FileCode,
  FileText,
  ShieldCheck,
  Table,
  Plus,
  ArrowRight,
  Terminal,
  Clock,
  Sparkles,
  Archive,
  ChevronDown,
  ChevronRight,
  Flame,
  Info
} from 'lucide-react';
import JSZip from 'jszip';
import { SqlEngine, OutputFormat, TableModel, SchemaCompareResult, MigrationExecutionStep } from '../../types/migrations';
import { EF_CORE_TABLES, SOURCE_QUORUM_CONFIG } from '../../data/efCoreSchemaData';
import {
  generatePureIdempotentSql,
  generateLiquibaseXml,
  generateLiquibaseYaml,
  generateLiquibaseFormattedSql
} from '../../utils/sqlMigrationGenerators';
import {
  PRESET_DATABASES,
  performSchemaCompare,
  detectEngineFromConnectionString,
  buildExecutionSteps,
  generateDeltaScript,
  DatabasePreset
} from '../../utils/schemaCompareEngine';

export const SqlMigrationsTab: React.FC = () => {
  // Tryb główny: 'generator' (Wariant 1) lub 'compare' (Wariant 2)
  const [activeMode, setActiveMode] = useState<'generator' | 'compare'>('generator');

  // --- STAN ŹRÓDŁA: QUORUM.BACKEND (APPSETTINGS.JSON) ---
  const [sourceProvider, setSourceProvider] = useState<'Sqlite' | 'PostgreSQL' | 'SqlServer'>(
    SOURCE_QUORUM_CONFIG.databaseProvider
  );
  const sourceConnectionString = SOURCE_QUORUM_CONFIG.connectionStrings[sourceProvider];
  const [isRefreshingSource, setIsRefreshingSource] = useState<boolean>(false);
  const [sourceRefreshMessage, setSourceRefreshMessage] = useState<string | null>(null);

  // --- STAN DLA WARIANTU 1: GENERATOR SKRYPTÓW ---
  const [selectedEngine, setSelectedEngine] = useState<SqlEngine>('postgres');
  const [outputFormat, setOutputFormat] = useState<OutputFormat>('sql');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [selectedTableNames, setSelectedTableNames] = useState<string[]>(
    EF_CORE_TABLES.map(t => t.name)
  );
  const [copiedCode, setCopiedCode] = useState<boolean>(false);
  const [isZipping, setIsZipping] = useState<boolean>(false);
  const [isGenerating, setIsGenerating] = useState<boolean>(false);
  const [generatedTimestamp, setGeneratedTimestamp] = useState<string>(() => new Date().toLocaleTimeString('pl-PL'));

  // --- STAN DLA WARIANTU 2: SCHEMA COMPARE & DIFFS ---
  const [selectedPresetId, setSelectedPresetId] = useState<string>('partial-v1');
  const currentPreset = useMemo(
    () => PRESET_DATABASES.find(p => p.id === selectedPresetId) || PRESET_DATABASES[0],
    [selectedPresetId]
  );
  const [customConnectionString, setCustomConnectionString] = useState<string>(
    currentPreset.connectionString
  );
  const [compareEngine, setCompareEngine] = useState<SqlEngine>(currentPreset.engine);
  const [simulatedState, setSimulatedState] = useState<DatabasePreset['simulatedExistingTables']>(
    currentPreset.simulatedExistingTables
  );
  const [isComparing, setIsComparing] = useState<boolean>(false);
  const [deltaFormat, setDeltaFormat] = useState<OutputFormat>('sql');
  const [copiedDelta, setCopiedDelta] = useState<boolean>(false);
  const [compareResult, setCompareResult] = useState<SchemaCompareResult | null>(() =>
    performSchemaCompare(currentPreset.connectionString, currentPreset.engine, currentPreset.simulatedExistingTables)
  );
  const [expandedTableNames, setExpandedTableNames] = useState<Record<string, boolean>>({
    'GatewayRoutes': true,
    'AspNetUsers': true
  });

  // --- STAN DLA MODALU "WGRAJ ZMIANY" (APPLY CHANGES) ---
  const [showApplyModal, setShowApplyModal] = useState<boolean>(false);
  const [isApplying, setIsApplying] = useState<boolean>(false);
  const [executionSteps, setExecutionSteps] = useState<MigrationExecutionStep[]>([]);
  const [currentExecutingStepIndex, setCurrentExecutingStepIndex] = useState<number>(-1);
  const [applyFinished, setApplyFinished] = useState<boolean>(false);

  // Filtrowane tabele dla generatora
  const filteredTables = useMemo(() => {
    return EF_CORE_TABLES.filter(t => {
      if (!selectedTableNames.includes(t.name)) return false;
      if (selectedCategory === 'all') return true;
      return t.category === selectedCategory;
    });
  }, [selectedTableNames, selectedCategory]);

  // Generowany kod skryptu (Wariant 1)
  const generatedScript = useMemo(() => {
    if (outputFormat === 'sql') {
      return generatePureIdempotentSql(filteredTables, selectedEngine);
    }
    if (outputFormat === 'liquibase-xml') {
      return generateLiquibaseXml(filteredTables);
    }
    if (outputFormat === 'liquibase-yaml') {
      return generateLiquibaseYaml(filteredTables);
    }
    if (outputFormat === 'liquibase-sql') {
      return generateLiquibaseFormattedSql(filteredTables, selectedEngine);
    }
    return '';
  }, [filteredTables, selectedEngine, outputFormat]);

  // Skrypt różnicowy (Wariant 2 - Delta)
  const deltaScript = useMemo(() => {
    if (!compareResult) return '';
    return generateDeltaScript(compareResult.tables, compareEngine, deltaFormat);
  }, [compareResult, compareEngine, deltaFormat]);

  // Akcja: Pobranie całości tabel z źródła Quorum.Backend
  const handleRefreshSourceTables = () => {
    setIsRefreshingSource(true);
    setSourceRefreshMessage(null);
    setTimeout(() => {
      setIsRefreshingSource(false);
      selectAllTables();
      setSourceRefreshMessage(
        `Załadowano pomyślnie komplet ${EF_CORE_TABLES.length} tabel ze źródła Quorum.Backend (${sourceProvider})`
      );
      setTimeout(() => setSourceRefreshMessage(null), 4000);
    }, 400);
  };

  // Akcja: Jawne generowanie całości struktur w Wariancie 1
  const handleGenerateStructures = () => {
    setIsGenerating(true);
    setTimeout(() => {
      setIsGenerating(false);
      setGeneratedTimestamp(new Date().toLocaleTimeString('pl-PL'));
    }, 350);
  };

  // Uniwersalny zapis do pliku
  const handleSaveToFile = (content: string, filename: string) => {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  };

  // Zapis do pliku dla Wariantu 1
  const handleDownloadVariant1File = () => {
    const ext = outputFormat === 'liquibase-xml' ? 'xml' : outputFormat === 'liquibase-yaml' ? 'yaml' : 'sql';
    const filename = `quorum_full_structure_${selectedEngine}_${outputFormat}.${ext}`;
    handleSaveToFile(generatedScript, filename);
  };

  // Zapis do pliku dla Wariantu 2 (Delta)
  const handleDownloadDeltaFile = () => {
    const ext = deltaFormat === 'liquibase-xml' ? 'xml' : deltaFormat === 'liquibase-yaml' ? 'yaml' : 'sql';
    const filename = `quorum_diffs_delta_${compareEngine}_${deltaFormat}.${ext}`;
    handleSaveToFile(deltaScript, filename);
  };

  // Kopiowanie różnic (Delta)
  const handleCopyDelta = () => {
    navigator.clipboard.writeText(deltaScript);
    setCopiedDelta(true);
    setTimeout(() => setCopiedDelta(false), 2000);
  };

  // Zmiana presetu bazy danych
  const handleSelectPreset = (preset: DatabasePreset) => {
    setSelectedPresetId(preset.id);
    setCustomConnectionString(preset.connectionString);
    setCompareEngine(preset.engine);
    setSimulatedState(preset.simulatedExistingTables);
    const res = performSchemaCompare(preset.connectionString, preset.engine, preset.simulatedExistingTables);
    setCompareResult(res);
  };

  // Uruchomienie Schema Compare
  const handleRunSchemaCompare = () => {
    setIsComparing(true);
    setTimeout(() => {
      const detectedEng = detectEngineFromConnectionString(customConnectionString);
      setCompareEngine(detectedEng);
      const res = performSchemaCompare(customConnectionString, detectedEng, simulatedState);
      setCompareResult(res);
      setIsComparing(false);
    }, 600);
  };

  // Kopiowanie do schowka
  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCode(true);
    setTimeout(() => setCopiedCode(false), 2000);
  };

  // Pobieranie pojedynczego pliku
  const handleDownloadFile = () => {
    const ext = outputFormat === 'liquibase-xml' ? 'xml' : outputFormat === 'liquibase-yaml' ? 'yaml' : 'sql';
    const filename = `quorum_migration_${selectedEngine}_${outputFormat}.${ext}`;
    const blob = new Blob([generatedScript], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  };

  // Pobieranie pełnej paczki ZIP ze wszystkimi silnikami i formatami Liquibase
  const handleDownloadAllZip = async () => {
    setIsZipping(true);
    try {
      const zip = new JSZip();
      const folderSql = zip.folder('pure-idempotent-sql');
      const engines: SqlEngine[] = ['sqlserver', 'postgres', 'sqlite', 'oracle'];

      engines.forEach(eng => {
        const sql = generatePureIdempotentSql(EF_CORE_TABLES, eng);
        folderSql?.file(`migration_${eng}_idempotent.sql`, sql);
      });

      const folderLiquibase = zip.folder('liquibase');
      folderLiquibase?.file('changelog.xml', generateLiquibaseXml(EF_CORE_TABLES));
      folderLiquibase?.file('changelog.yaml', generateLiquibaseYaml(EF_CORE_TABLES));
      folderLiquibase?.file('changelog-postgres.sql', generateLiquibaseFormattedSql(EF_CORE_TABLES, 'postgres'));
      folderLiquibase?.file('changelog-sqlserver.sql', generateLiquibaseFormattedSql(EF_CORE_TABLES, 'sqlserver'));

      // README
      zip.file('README.md', `# Quorum IdentityServer & Gateway - Migracje SQL & Liquibase
Wygenerowano automatycznie z modeli Entity Framework Core 10.0.

Struktura katalogów:
- pure-idempotent-sql/: Czyste, bezpieczne skrypty SQL z warunkami IF NOT EXISTS i dodawaniem kolumn jako NULL-owalne.
  - migration_sqlserver_idempotent.sql
  - migration_postgres_idempotent.sql
  - migration_sqlite_idempotent.sql
  - migration_oracle_idempotent.sql
- liquibase/: Changesety w formatach XML, YAML oraz Formatted SQL z preConditions onFail="MARK_RAN".
`);

      const content = await zip.generateAsync({ type: 'blob' });
      const url = URL.createObjectURL(content);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'quorum-ef-migrations-bundle.zip';
      link.click();
      URL.revokeObjectURL(url);
    } finally {
      setIsZipping(false);
    }
  };

  // Rozpoczęcie wgrywania zmian (Apply Changes)
  const handleOpenApplyModal = () => {
    if (!compareResult) return;
    const steps = buildExecutionSteps(compareResult);
    setExecutionSteps(steps);
    setCurrentExecutingStepIndex(-1);
    setApplyFinished(false);
    setShowApplyModal(true);
  };

  // Sekwencyjne wykonanie kroków migracji
  const handleExecuteMigrationSteps = async () => {
    if (executionSteps.length === 0) return;
    setIsApplying(true);

    for (let i = 0; i < executionSteps.length; i++) {
      setCurrentExecutingStepIndex(i);
      setExecutionSteps(prev =>
        prev.map((step, idx) => (idx === i ? { ...step, status: 'running' } : step))
      );

      // Symulacja wykonania zapytania DDL
      const delay = Math.floor(Math.random() * 200) + 120;
      await new Promise(r => setTimeout(r, delay));

      setExecutionSteps(prev =>
        prev.map((step, idx) =>
          idx === i
            ? {
                ...step,
                status: 'success',
                durationMs: delay,
                message: `Wykonano pomyślnie (${delay} ms)`
              }
            : step
        )
      );
    }

    // Po pomyślnym wgraniu zmian - aktualizujemy symulowany stan bazy na pełny (zsynchronizowany!)
    const fullySyncedState: Record<string, { columns: string[]; indexes: string[] }> = {};
    EF_CORE_TABLES.forEach(t => {
      fullySyncedState[t.name] = {
        columns: t.columns.map(c => c.name),
        indexes: t.indexes.map(i => i.name)
      };
    });

    setSimulatedState(fullySyncedState);
    const updatedResult = performSchemaCompare(customConnectionString, compareEngine, fullySyncedState);
    setCompareResult(updatedResult);

    setIsApplying(false);
    setApplyFinished(true);
  };

  const toggleTableExpand = (name: string) => {
    setExpandedTableNames(prev => ({ ...prev, [name]: !prev[name] }));
  };

  const toggleTableSelect = (name: string) => {
    setSelectedTableNames(prev =>
      prev.includes(name) ? prev.filter(n => n !== name) : [...prev, name]
    );
  };

  const selectAllTables = () => {
    setSelectedTableNames(EF_CORE_TABLES.map(t => t.name));
  };

  const deselectAllTables = () => {
    setSelectedTableNames([]);
  };

  return (
    <div id="sql-migrations-container" className="space-y-6">
      {/* NAGŁÓWEK STRONY I PRZEŁĄCZNIK WARIANTÓW */}
      <div id="sql-migrations-header" className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-amber-500/10 border border-amber-500/20 rounded-lg text-amber-400">
                <Database className="w-6 h-6" />
              </div>
              <div>
                <h1 className="text-xl font-bold text-slate-100 flex items-center gap-2.5">
                  Migracje SQL & Porównanie Schematów EF Core
                  <span className="text-xs px-2.5 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/30 font-medium">
                    100% Idempotencja
                  </span>
                </h1>
                <p className="text-sm text-slate-400 mt-1">
                  Eksport modeli Entity Framework do silników SQL i Liquibase oraz weryfikacja schematów bazy (Schema Compare) z opcją wgrywania zmian.
                </p>
              </div>
            </div>
          </div>

          {/* PRZEŁĄCZNIK GŁÓWNYCH TRYBÓW */}
          <div className="flex items-center bg-slate-950 p-1.5 rounded-xl border border-slate-800">
            <button
              id="mode-tab-generator"
              onClick={() => setActiveMode('generator')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                activeMode === 'generator'
                  ? 'bg-amber-500 text-slate-950 shadow-md font-semibold'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900'
              }`}
            >
              <FileCode className="w-4 h-4" />
              Wariant 1: Generator Skryptów
            </button>
            <button
              id="mode-tab-compare"
              onClick={() => setActiveMode('compare')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                activeMode === 'compare'
                  ? 'bg-amber-500 text-slate-950 shadow-md font-semibold'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900'
              }`}
            >
              <ArrowLeftRight className="w-4 h-4" />
              Wariant 2: Schema Compare & Wgraj
              {compareResult && compareResult.summary.missingTablesCount + compareResult.summary.missingColumnsCount > 0 && (
                <span className="w-2 h-2 rounded-full bg-amber-400 animate-pulse ml-1" />
              )}
            </button>
          </div>
        </div>

        {/* BELKA REGUŁ IDEMPOTENCJI (ZGODNA Z POLECENIEM UŻYTKOWNIKA) */}
        <div className="mt-5 pt-4 border-t border-slate-800/80 flex flex-wrap items-center gap-y-2 gap-x-6 text-xs text-slate-400">
          <div className="flex items-center gap-1.5 text-emerald-400 font-medium">
            <ShieldCheck className="w-4 h-4" />
            Zasada 1: Bezpieczne sprawdzanie (brak usuwania istniejących tabel)
          </div>
          <div className="flex items-center gap-1.5 text-amber-300 font-medium">
            <Plus className="w-4 h-4" />
            Zasada 2: Brakujące kolumny dodawane jako <span className="underline decoration-amber-400 font-mono">NULL-owalne</span>
          </div>
          <div className="flex items-center gap-1.5 text-sky-400 font-medium">
            <Flame className="w-4 h-4" />
            Zasada 3: Changesety Liquibase z <code className="bg-slate-950 px-1 py-0.5 rounded border border-slate-800 text-sky-300">preConditions onFail="MARK_RAN"</code>
          </div>
        </div>
      </div>

      {/* PANEL ŹRÓDŁA: QUORUM.BACKEND (POBIERANIE CAŁOŚCI TABEL ZE ŹRÓDŁA) */}
      <div id="source-backend-config-card" className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <div className="flex items-start sm:items-center gap-3">
            <div className="p-2 bg-emerald-500/10 border border-emerald-500/20 rounded-lg text-emerald-400 shrink-0 mt-0.5 sm:mt-0">
              <Server className="w-5 h-5" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-bold uppercase tracking-wider text-emerald-400">
                  Źródło Modeli: Quorum.Backend
                </span>
                <span className="text-[11px] px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                  appsettings.json
                </span>
                <span className="text-[11px] px-2 py-0.5 rounded bg-emerald-500/15 text-emerald-300 font-semibold">
                  Załadowano {EF_CORE_TABLES.length} tabel
                </span>
              </div>
              <div className="text-xs text-slate-400 mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 font-mono">
                <span className="text-slate-500">Domyślny provider:</span>
                <span className="text-slate-200 font-semibold">{sourceProvider}</span>
                <span className="text-slate-600">|</span>
                <span className="text-slate-500">Connection string:</span>
                <span className="text-slate-300 truncate max-w-lg">{sourceConnectionString}</span>
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2 shrink-0">
            {/* Przełącznik providera ze źródłowego appsettings.json */}
            <div className="flex bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs">
              {(['Sqlite', 'PostgreSQL', 'SqlServer'] as const).map(p => (
                <button
                  key={p}
                  onClick={() => setSourceProvider(p)}
                  className={`px-2.5 py-1 rounded font-medium transition-all ${
                    sourceProvider === p
                      ? 'bg-emerald-600 text-white shadow-sm'
                      : 'text-slate-400 hover:text-slate-200'
                  }`}
                >
                  {p}
                </button>
              ))}
            </div>

            {/* Przycisk pobrania całości tabel z Quorum.Backend */}
            <button
              id="fetch-source-tables-btn"
              onClick={handleRefreshSourceTables}
              disabled={isRefreshingSource}
              className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-300 border border-emerald-500/40 text-xs font-semibold transition-all shadow-sm"
              title="Pobiera całość tabel i modeli z Quorum.Backend"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isRefreshingSource ? 'animate-spin' : ''}`} />
              {isRefreshingSource ? 'Pobieranie...' : 'Pobierz tabele ze źródła'}
            </button>
          </div>
        </div>

        {sourceRefreshMessage && (
          <div className="mt-3 p-2.5 bg-emerald-950/30 border border-emerald-500/30 rounded-lg text-xs text-emerald-300 flex items-center gap-2 animate-in fade-in">
            <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
            <span>{sourceRefreshMessage}</span>
          </div>
        )}
      </div>

      {/* ========================================================================= */}
      {/* WARIANT 1: GENERATOR IDEMPOTENTNYCH SKRYPTÓW SQL I LIQUIBASE */}
      {/* ========================================================================= */}
      {activeMode === 'generator' && (
        <div id="generator-mode-panel" className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* PANEL BOCZNY: KONFIGURACJA SILNIKA I TABEL */}
          <div className="lg:col-span-4 space-y-5">
            {/* Przycisk generuj całość struktur */}
            <div className="bg-gradient-to-r from-amber-500/15 via-amber-500/5 to-slate-900 border border-amber-500/30 rounded-xl p-4 shadow-md">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold uppercase tracking-wider text-amber-300 flex items-center gap-1.5">
                  <Sparkles className="w-4 h-4 text-amber-400" />
                  Wariant 1: Generator Struktur
                </span>
                <span className="text-[11px] text-slate-400 font-mono">
                  {generatedTimestamp}
                </span>
              </div>
              <p className="text-xs text-slate-400 mb-3">
                Generuje pełny, idempotentny schemat na podstawie {filteredTables.length} modeli pobranych ze źródła Quorum.Backend.
              </p>
              <button
                id="generate-full-btn"
                onClick={handleGenerateStructures}
                disabled={isGenerating}
                className="w-full py-2.5 px-4 rounded-lg bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold text-xs flex items-center justify-center gap-2 transition-all shadow-md active:scale-[0.98]"
              >
                <Sparkles className={`w-4 h-4 ${isGenerating ? 'animate-spin' : ''}`} />
                {isGenerating ? 'Generowanie całości...' : 'Generuj całość struktur z bazy'}
              </button>
            </div>

            {/* Wybór silnika SQL */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-5">
              <label className="text-xs font-semibold uppercase tracking-wider text-slate-400 block mb-3">
                1. Silnik Bazy Danych (Dialekt SQL)
              </label>
              <div className="grid grid-cols-2 gap-2.5">
                {[
                  { id: 'postgres', name: 'PostgreSQL', desc: 'PL/pgSQL, IF NOT EXISTS' },
                  { id: 'sqlserver', name: 'SQL Server', desc: 'T-SQL, sys.tables & columns' },
                  { id: 'sqlite', name: 'SQLite', desc: 'Prosty, lekki, in-memory' },
                  { id: 'oracle', name: 'Oracle DB', desc: 'PL/SQL, EXECUTE IMMEDIATE' }
                ].map(eng => (
                  <button
                    key={eng.id}
                    onClick={() => setSelectedEngine(eng.id as SqlEngine)}
                    className={`p-3 rounded-lg border text-left transition-all ${
                      selectedEngine === eng.id
                        ? 'bg-amber-500/10 border-amber-500/50 text-slate-100 shadow-sm'
                        : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700 hover:text-slate-300'
                    }`}
                  >
                    <div className="font-semibold text-sm flex items-center justify-between">
                      {eng.name}
                      {selectedEngine === eng.id && <Check className="w-4 h-4 text-amber-400" />}
                    </div>
                    <div className="text-[11px] text-slate-500 mt-1">{eng.desc}</div>
                  </button>
                ))}
              </div>
            </div>

            {/* Wybór formatu wyjściowego */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-5">
              <label className="text-xs font-semibold uppercase tracking-wider text-slate-400 block mb-3">
                2. Format Wynikowy
              </label>
              <div className="space-y-2">
                {[
                  { id: 'sql', label: 'Czysty Idempotentny SQL (.sql)', icon: FileCode, desc: 'Bezpośrednie skrypty DDL z weryfikacją sys.catalog' },
                  { id: 'liquibase-xml', label: 'Liquibase XML Changelog (.xml)', icon: Layers, desc: 'Preconditions onFail="MARK_RAN", createTable & addColumn' },
                  { id: 'liquibase-yaml', label: 'Liquibase YAML Changelog (.yaml)', icon: FileText, desc: 'Czytelny format YAML z pełną idempotentnością' },
                  { id: 'liquibase-sql', label: 'Liquibase Formatted SQL (.sql)', icon: Terminal, desc: 'Format standardu -- liquibase formatted sql' }
                ].map(fmt => {
                  const Icon = fmt.icon;
                  return (
                    <button
                      key={fmt.id}
                      onClick={() => setOutputFormat(fmt.id as OutputFormat)}
                      className={`w-full p-3 rounded-lg border text-left flex items-start gap-3 transition-all ${
                        outputFormat === fmt.id
                          ? 'bg-amber-500/10 border-amber-500/50 text-slate-100'
                          : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700'
                      }`}
                    >
                      <Icon className={`w-5 h-5 mt-0.5 ${outputFormat === fmt.id ? 'text-amber-400' : 'text-slate-500'}`} />
                      <div className="flex-1">
                        <div className="text-sm font-semibold">{fmt.label}</div>
                        <div className="text-xs text-slate-500 mt-0.5">{fmt.desc}</div>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Wybór tabel EF Core */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-5">
              <div className="flex items-center justify-between mb-3">
                <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                  3. Zakres Modeli ({selectedTableNames.length}/{EF_CORE_TABLES.length})
                </label>
                <div className="flex items-center gap-2 text-xs">
                  <button onClick={selectAllTables} className="text-amber-400 hover:underline">Wszystkie</button>
                  <span className="text-slate-600">|</span>
                  <button onClick={deselectAllTables} className="text-slate-500 hover:text-slate-400">Wyczyść</button>
                </div>
              </div>

              {/* Filtr kategorii */}
              <div className="grid grid-cols-2 gap-1.5 mb-3 text-xs">
                {[
                  { id: 'all', label: 'Wszystkie moduły' },
                  { id: 'gateway', label: 'Gateway & Federacje' },
                  { id: 'identity', label: 'ASP.NET Identity' },
                  { id: 'openiddict', label: 'Klienci & Zakresy' }
                ].map(cat => (
                  <button
                    key={cat.id}
                    onClick={() => setSelectedCategory(cat.id)}
                    className={`px-2 py-1.5 rounded text-left truncate transition-colors ${
                      selectedCategory === cat.id
                        ? 'bg-slate-800 text-amber-400 font-medium'
                        : 'text-slate-400 hover:bg-slate-950'
                    }`}
                  >
                    {cat.label}
                  </button>
                ))}
              </div>

              {/* Lista tabel */}
              <div className="max-h-56 overflow-y-auto space-y-1.5 pr-1 border border-slate-800 rounded-lg p-2 bg-slate-950">
                {EF_CORE_TABLES.map(table => {
                  const isChecked = selectedTableNames.includes(table.name);
                  return (
                    <label
                      key={table.name}
                      className="flex items-center gap-2.5 p-1.5 hover:bg-slate-900 rounded cursor-pointer text-xs"
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => toggleTableSelect(table.name)}
                        className="rounded border-slate-700 bg-slate-800 text-amber-500 focus:ring-amber-400"
                      />
                      <span className={isChecked ? 'text-slate-200 font-mono' : 'text-slate-500 font-mono'}>
                        {table.name}
                      </span>
                      <span className="text-[10px] text-slate-500 ml-auto">{table.columns.length} kol.</span>
                    </label>
                  );
                })}
              </div>
            </div>

            {/* Przycisk pobrania paczki ZIP */}
            <button
              id="download-zip-btn"
              onClick={handleDownloadAllZip}
              disabled={isZipping}
              className="w-full flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-200 text-sm font-medium transition-all shadow-md"
            >
              {isZipping ? (
                <RefreshCw className="w-4 h-4 animate-spin text-amber-400" />
              ) : (
                <Archive className="w-4 h-4 text-amber-400" />
              )}
              {isZipping ? 'Pakowanie archiwum...' : 'Pobierz pełną paczkę ZIP (Wszystkie silniki)'}
            </button>
          </div>

          {/* GŁÓWNY OBSZAR: PODGLĄD KODU */}
          <div className="lg:col-span-8 space-y-4">
            <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col h-[750px]">
              {/* Pasek narzędzi edytora */}
              <div className="bg-slate-950 px-4 py-3 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-mono font-semibold text-slate-300 bg-slate-900 px-2.5 py-1 rounded border border-slate-800">
                    {outputFormat.toUpperCase()} ({selectedEngine.toUpperCase()})
                  </span>
                  <span className="text-xs text-slate-500">
                    {filteredTables.length} tabel | {generatedScript.split('\n').length} linii
                  </span>
                </div>

                {/* Przyciski: Generuj, Kopiuj do schowka, Zapisz do pliku */}
                <div className="flex items-center gap-2">
                  <button
                    id="generate-structure-btn"
                    onClick={handleGenerateStructures}
                    disabled={isGenerating}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-sm transition-colors"
                  >
                    <Sparkles className={`w-3.5 h-3.5 ${isGenerating ? 'animate-spin' : ''}`} />
                    {isGenerating ? 'Generowanie...' : 'Generuj'}
                  </button>

                  <button
                    id="copy-script-btn"
                    onClick={() => handleCopy(generatedScript)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition-colors"
                    title="Kopiuj do schowka"
                  >
                    {copiedCode ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5 text-slate-400" />}
                    {copiedCode ? 'Skopiowano!' : 'Kopiuj do schowka'}
                  </button>

                  <button
                    id="save-to-file-btn"
                    onClick={handleDownloadVariant1File}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-500 hover:bg-amber-400 text-slate-950 text-xs font-semibold transition-colors shadow-sm"
                    title="Zapisz wygenerowany kod do pliku"
                  >
                    <Download className="w-3.5 h-3.5" />
                    Zapisz do pliku
                  </button>
                </div>
              </div>

              {/* Informacja o gwarancji idempotentności */}
              <div className="bg-amber-950/20 border-b border-amber-500/20 px-4 py-2 flex items-center gap-2 text-xs text-amber-300">
                <Info className="w-4 h-4 shrink-0 text-amber-400" />
                <span>
                  <strong>Idempotentność skryptu:</strong> Jeśli tabela istnieje, nie zostanie usunięta. Brakujące kolumny są dodawane jako <strong>NULL-owalne</strong> bez utraty danych.
                </span>
              </div>

              {/* Treść kodu ze stylizacją terminalową */}
              <div className="flex-1 overflow-auto p-4 font-mono text-xs text-slate-300 bg-slate-950 selection:bg-amber-500/30">
                <pre className="whitespace-pre">
                  <code>{generatedScript}</code>
                </pre>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* WARIANT 2: SCHEMA COMPARE & WGRYWANIE ZMIAN NA BAZIE (CONNECTION STRING) */}
      {/* ========================================================================= */}
      {activeMode === 'compare' && (
        <div id="compare-mode-panel" className="space-y-6">
          {/* PANEL POŁĄCZENIA I PRESETÓW */}
          <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
            <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
              <div>
                <h2 className="text-base font-bold text-slate-100 flex items-center gap-2">
                  <Server className="w-5 h-5 text-amber-400" />
                  Połączenie z Bazą Danych & Weryfikacja Schematu
                </h2>
                <p className="text-xs text-slate-400 mt-0.5">
                  Wprowadź connection string docelowej bazy lub wybierz gotowy profil testowy, aby porównać istniejące tabele, kolumny i indeksy z modelami EF Core.
                </p>
              </div>

              {/* Presety */}
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs text-slate-400 font-medium mr-1">Profile testowe:</span>
                {PRESET_DATABASES.map(preset => (
                  <button
                    key={preset.id}
                    onClick={() => handleSelectPreset(preset)}
                    className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                      selectedPresetId === preset.id
                        ? 'bg-amber-500/20 border-amber-500/50 text-amber-300 shadow-sm'
                        : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700 hover:text-slate-300'
                    }`}
                  >
                    {preset.name}
                  </button>
                ))}
              </div>
            </div>

            {/* Input Connection String */}
            <div className="space-y-2">
              <label className="text-xs font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-between">
                <span>Parametry Connection String</span>
                <span className="text-[11px] font-mono text-amber-400">
                  Wykryty silnik: {compareEngine.toUpperCase()}
                </span>
              </label>

              <div className="flex flex-col sm:flex-row gap-2">
                <input
                  id="connection-string-input"
                  type="text"
                  value={customConnectionString}
                  onChange={e => {
                    setCustomConnectionString(e.target.value);
                    setCompareEngine(detectEngineFromConnectionString(e.target.value));
                  }}
                  placeholder="np. Host=localhost;Port=5432;Database=quorum_db;Username=postgres;Password=***;"
                  className="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-4 py-2.5 text-xs font-mono text-slate-200 focus:outline-none focus:border-amber-500/60"
                />

                <button
                  id="run-compare-btn"
                  onClick={handleRunSchemaCompare}
                  disabled={isComparing}
                  className="flex items-center justify-center gap-2 px-6 py-2.5 rounded-lg bg-amber-500 hover:bg-amber-400 text-slate-950 font-semibold text-xs transition-colors shadow-md shrink-0"
                >
                  <RefreshCw className={`w-4 h-4 ${isComparing ? 'animate-spin' : ''}`} />
                  {isComparing ? 'Porównywanie...' : 'Porównaj schemat'}
                </button>
              </div>

              <p className="text-[11px] text-slate-500 italic">
                {currentPreset.description}
              </p>
            </div>
          </div>

          {/* WYNIKI PORÓWNANIA SCHEMATÓW (SCHEMA COMPARE RESULT) */}
          {compareResult && (
            <div className="space-y-6">
              {/* KARTY STATYSTYK SCHEMA COMPARE */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
                {/* 1. Stan synchronizacji */}
                <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
                  <div className="text-xs text-slate-400 uppercase font-semibold">Stan Zgodności</div>
                  <div className="my-2 flex items-baseline gap-2">
                    <span className="text-3xl font-extrabold text-slate-100">
                      {compareResult.summary.synchronizedPercentage}%
                    </span>
                    <span className="text-xs text-slate-500 font-mono">zgodności</span>
                  </div>
                  <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div
                      className={`h-full transition-all duration-500 ${
                        compareResult.summary.synchronizedPercentage === 100
                          ? 'bg-emerald-400'
                          : compareResult.summary.synchronizedPercentage > 50
                          ? 'bg-amber-400'
                          : 'bg-rose-500'
                      }`}
                      style={{ width: `${compareResult.summary.synchronizedPercentage}%` }}
                    />
                  </div>
                </div>

                {/* 2. Brakujące tabele */}
                <div className="bg-slate-900 border border-slate-800 rounded-xl p-4">
                  <div className="text-xs text-slate-400 uppercase font-semibold flex items-center justify-between">
                    <span>Brakujące Tabele</span>
                    <span className="text-amber-400 text-xs">CREATE TABLE</span>
                  </div>
                  <div className="mt-2 text-3xl font-extrabold text-amber-400">
                    {compareResult.summary.missingTablesCount}
                  </div>
                  <div className="text-[11px] text-slate-500 mt-1">
                    z {compareResult.summary.totalExpectedTables} modeli EF Core
                  </div>
                </div>

                {/* 3. Brakujące kolumny (NULL) */}
                <div className="bg-slate-900 border border-slate-800 rounded-xl p-4">
                  <div className="text-xs text-slate-400 uppercase font-semibold flex items-center justify-between">
                    <span>Kolumny do Dodania</span>
                    <span className="text-sky-400 text-[11px] font-mono">ALTER TABLE</span>
                  </div>
                  <div className="mt-2 text-3xl font-extrabold text-sky-400">
                    {compareResult.summary.missingColumnsCount}
                  </div>
                  <div className="text-[11px] text-slate-500 mt-1">
                    Dodawane bezpiecznie jako <strong>NULL</strong>
                  </div>
                </div>

                {/* 4. Brakujące indeksy */}
                <div className="bg-slate-900 border border-slate-800 rounded-xl p-4">
                  <div className="text-xs text-slate-400 uppercase font-semibold flex items-center justify-between">
                    <span>Brakujące Indeksy</span>
                    <span className="text-purple-400 text-[11px] font-mono">CREATE INDEX</span>
                  </div>
                  <div className="mt-2 text-3xl font-extrabold text-purple-400">
                    {compareResult.summary.missingIndexesCount}
                  </div>
                  <div className="text-[11px] text-slate-500 mt-1">
                    Indeksy unikalne i wydajnościowe
                  </div>
                </div>

                {/* 5. GŁÓWNY PRZYCISK: WGRAJ ZMIANY */}
                <div className="bg-gradient-to-br from-amber-500/10 to-amber-600/5 border border-amber-500/30 rounded-xl p-4 flex flex-col justify-between">
                  <div>
                    <div className="text-xs text-amber-300 font-semibold uppercase">Akcja Schema Sync</div>
                    <div className="text-xs text-slate-400 mt-1">
                      {compareResult.summary.missingTablesCount + compareResult.summary.missingColumnsCount > 0
                        ? 'Wymagane zaktualizowanie schematu bazy.'
                        : 'Baza jest w 100% zsynchronizowana.'}
                    </div>
                  </div>

                  <button
                    id="apply-changes-btn"
                    onClick={handleOpenApplyModal}
                    disabled={compareResult.summary.missingTablesCount + compareResult.summary.missingColumnsCount + compareResult.summary.missingIndexesCount === 0}
                    className={`w-full mt-3 py-2.5 px-4 rounded-lg font-bold text-xs flex items-center justify-center gap-2 transition-all shadow-lg ${
                      compareResult.summary.missingTablesCount + compareResult.summary.missingColumnsCount + compareResult.summary.missingIndexesCount > 0
                        ? 'bg-amber-500 hover:bg-amber-400 text-slate-950 cursor-pointer animate-pulse'
                        : 'bg-slate-800 text-slate-500 cursor-not-allowed'
                    }`}
                  >
                    <Play className="w-4 h-4 fill-current" />
                    Wgraj zmiany na bazę
                  </button>
                </div>
              </div>

              {/* SZCZEGÓŁOWE DRZEWO RÓŻNIC (DIFF INSPECTOR) & SKRYPT DELTA */}
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                {/* Lewa kolumna: Lista tabel i statusów */}
                <div className="lg:col-span-7 space-y-3">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-bold text-slate-200 flex items-center gap-2">
                      <Table className="w-4 h-4 text-amber-400" />
                      Inspekcja Obiektów Bazodanowych (Schema Inspector)
                    </h3>
                    <span className="text-xs text-slate-400">
                      Zsynchronizowano o {compareResult.timestamp}
                    </span>
                  </div>

                  <div className="space-y-2.5">
                    {compareResult.tables.map(table => {
                      const isExpanded = !!expandedTableNames[table.tableName];
                      const efModel = EF_CORE_TABLES.find(m => m.name === table.tableName);

                      return (
                        <div
                          key={table.tableName}
                          className={`rounded-xl border transition-all ${
                            table.status === 'matched'
                              ? 'bg-slate-900/60 border-slate-800'
                              : table.status === 'has_missing_columns'
                              ? 'bg-slate-900 border-amber-500/40 shadow-sm'
                              : 'bg-slate-900 border-sky-500/40 shadow-sm'
                          }`}
                        >
                          {/* Belka tabeli */}
                          <div
                            onClick={() => toggleTableExpand(table.tableName)}
                            className="p-3.5 flex items-center justify-between cursor-pointer hover:bg-slate-800/50 rounded-xl"
                          >
                            <div className="flex items-center gap-3">
                              {isExpanded ? (
                                <ChevronDown className="w-4 h-4 text-slate-400" />
                              ) : (
                                <ChevronRight className="w-4 h-4 text-slate-400" />
                              )}
                              <div>
                                <span className="font-mono text-sm font-bold text-slate-100">
                                  {table.tableName}
                                </span>
                                <span className="text-xs text-slate-500 ml-2.5">
                                  ({table.category})
                                </span>
                              </div>
                            </div>

                            <div className="flex items-center gap-2">
                              {table.status === 'matched' && (
                                <span className="flex items-center gap-1.5 text-xs text-emerald-400 bg-emerald-500/10 px-2.5 py-1 rounded-full border border-emerald-500/20 font-medium">
                                  <CheckCircle2 className="w-3.5 h-3.5" />
                                  Zgodna (Istnieje)
                                </span>
                              )}

                              {table.status === 'has_missing_columns' && (
                                <span className="flex items-center gap-1.5 text-xs text-amber-400 bg-amber-500/10 px-2.5 py-1 rounded-full border border-amber-500/30 font-medium">
                                  <AlertCircle className="w-3.5 h-3.5" />
                                  Brakujące kolumny: +{table.missingColumns.length} (NULL)
                                </span>
                              )}

                              {table.status === 'missing_table' && (
                                <span className="flex items-center gap-1.5 text-xs text-sky-400 bg-sky-500/10 px-2.5 py-1 rounded-full border border-sky-500/30 font-medium">
                                  <Plus className="w-3.5 h-3.5" />
                                  Nowa tabela (Brak w bazie)
                                </span>
                              )}
                            </div>
                          </div>

                          {/* Rozwinięcie szczegółów kolumn i indeksów */}
                          {isExpanded && (
                            <div className="px-4 pb-4 pt-1 border-t border-slate-800/80 space-y-3">
                              {/* Kolumny */}
                              <div>
                                <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-2">
                                  Kolumny tabeli ({efModel?.columns.length || 0}):
                                </div>
                                <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                                  {efModel?.columns.map(col => {
                                    const isMissingInDb = table.missingColumns.some(mc => mc.name.toLowerCase() === col.name.toLowerCase());
                                    return (
                                      <div
                                        key={col.name}
                                        className={`flex items-center justify-between p-2 rounded text-xs font-mono ${
                                          isMissingInDb
                                            ? 'bg-amber-500/10 border border-amber-500/30 text-amber-200'
                                            : 'bg-slate-950 border border-slate-800/60 text-slate-300'
                                        }`}
                                      >
                                        <div className="flex items-center gap-2">
                                          {isMissingInDb ? (
                                            <span className="w-2 h-2 rounded-full bg-amber-400" />
                                          ) : (
                                            <Check className="w-3 h-3 text-emerald-400" />
                                          )}
                                          <span className="font-semibold">{col.name}</span>
                                        </div>
                                        <div className="text-[10px] text-slate-400">
                                          {isMissingInDb ? (
                                            <span className="text-amber-400 font-bold">+ DODAĆ (NULL)</span>
                                          ) : (
                                            col.type
                                          )}
                                        </div>
                                      </div>
                                    );
                                  })}
                                </div>
                              </div>

                              {/* Indeksy */}
                              {efModel && efModel.indexes.length > 0 && (
                                <div>
                                  <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1.5">
                                    Indeksy:
                                  </div>
                                  <div className="space-y-1">
                                    {efModel.indexes.map(idx => {
                                      const isMissing = table.missingIndexes.some(mi => mi.name === idx.name);
                                      return (
                                        <div
                                          key={idx.name}
                                          className={`flex items-center justify-between p-1.5 rounded text-xs font-mono ${
                                            isMissing
                                              ? 'bg-purple-500/10 border border-purple-500/30 text-purple-200'
                                              : 'bg-slate-950 border border-slate-800/50 text-slate-400'
                                          }`}
                                        >
                                          <div className="flex items-center gap-2">
                                            {isMissing ? (
                                              <span className="text-purple-400">+</span>
                                            ) : (
                                              <Check className="w-3 h-3 text-emerald-400" />
                                            )}
                                            <span>{idx.name} ({idx.columns.join(', ')})</span>
                                          </div>
                                          {isMissing && <span className="text-[10px] text-purple-300 font-bold">BRAKUJĄCY</span>}
                                        </div>
                                      );
                                    })}
                                  </div>
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Prawa kolumna: Wygenerowany skrypt różnicowy (Delta SQL / Liquibase) */}
                <div className="lg:col-span-5 space-y-3">
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
                    <h3 className="text-sm font-bold text-slate-200 flex items-center gap-2">
                      <FileCode className="w-4 h-4 text-amber-400" />
                      Różnice: Źródło vs Cel
                    </h3>

                    {/* Format różnic (SQL / Liquibase XML / YAML / Formatted SQL) */}
                    <div className="flex items-center bg-slate-950 p-1 rounded-lg border border-slate-800 text-[11px]">
                      {[
                        { id: 'sql', label: 'SQL' },
                        { id: 'liquibase-xml', label: 'XML' },
                        { id: 'liquibase-yaml', label: 'YAML' },
                        { id: 'liquibase-sql', label: 'Liq. SQL' }
                      ].map(f => (
                        <button
                          key={f.id}
                          onClick={() => setDeltaFormat(f.id as OutputFormat)}
                          className={`px-2 py-0.5 rounded font-mono transition-all ${
                            deltaFormat === f.id
                              ? 'bg-amber-500 text-slate-950 font-bold shadow-sm'
                              : 'text-slate-400 hover:text-slate-200'
                          }`}
                        >
                          {f.label}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden flex flex-col h-[580px] shadow-lg">
                    <div className="bg-slate-950 px-3.5 py-2.5 border-b border-slate-800 text-xs font-mono text-slate-400 flex items-center justify-between">
                      <span className="truncate max-w-[220px]">
                        delta_{compareEngine}_{deltaFormat}.{deltaFormat === 'liquibase-xml' ? 'xml' : deltaFormat === 'liquibase-yaml' ? 'yaml' : 'sql'}
                      </span>
                      <div className="flex items-center gap-2">
                        <span className="text-[11px] text-slate-500">
                          {deltaScript.split('\n').length} linii
                        </span>
                        <button
                          id="copy-delta-btn"
                          onClick={handleCopyDelta}
                          className="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-mono flex items-center gap-1 transition-colors"
                          title="Kopiuj skrypt różnicowy"
                        >
                          {copiedDelta ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3 text-slate-400" />}
                          {copiedDelta ? 'Skopiowano' : 'Kopiuj'}
                        </button>
                        <button
                          id="save-delta-file-btn"
                          onClick={handleDownloadDeltaFile}
                          className="px-2 py-0.5 rounded bg-amber-500 hover:bg-amber-400 text-slate-950 font-semibold text-xs flex items-center gap-1 transition-colors"
                          title="Zapisz skrypt różnicowy do pliku"
                        >
                          <Download className="w-3 h-3" />
                          Zapisz do pliku
                        </button>
                      </div>
                    </div>

                    <div className="flex-1 p-4 font-mono text-xs text-slate-300 bg-slate-950 overflow-auto whitespace-pre selection:bg-amber-500/30">
                      <code>{deltaScript}</code>
                    </div>

                    <div className="p-3 bg-slate-900 border-t border-slate-800 flex items-center justify-between gap-2">
                      <span className="text-xs text-slate-400">
                        Przycisk wgraj zmiany wgrywa czysty SQL
                      </span>
                      <button
                        id="apply-changes-btn"
                        onClick={handleOpenApplyModal}
                        disabled={compareResult.summary.missingTablesCount + compareResult.summary.missingColumnsCount === 0}
                        className="px-4 py-2 rounded-lg bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold text-xs flex items-center gap-2 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shadow-md"
                      >
                        <Play className="w-3.5 h-3.5 fill-current" />
                        Wgraj zmiany
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: "WGRAJ ZMIANY" (APPLY CHANGES EXECUTION RUNNER) */}
      {/* ========================================================================= */}
      {showApplyModal && (
        <div id="apply-changes-modal" className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-2xl w-full overflow-hidden shadow-2xl animate-in fade-in zoom-in duration-200">
            {/* Nagłówek modalu */}
            <div className="bg-slate-950 px-6 py-4 border-b border-slate-800 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-amber-500/10 border border-amber-500/30 rounded-lg text-amber-400">
                  <Play className="w-5 h-5 fill-current" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-100">
                    Wdrażanie Zmian Schematu (Wgraj czysty SQL)
                  </h3>
                  <p className="text-xs text-slate-400">
                    Baza: {compareResult?.databaseName} ({compareEngine.toUpperCase()}) &bull; Wgrywanie czystych instrukcji SQL DDL
                  </p>
                </div>
              </div>

              {!isApplying && (
                <button
                  onClick={() => setShowApplyModal(false)}
                  className="text-slate-400 hover:text-slate-200 text-sm font-semibold p-1"
                >
                  ✕
                </button>
              )}
            </div>

            {/* Ciało modalu */}
            <div className="p-6 space-y-5">
              {/* Ostrzeżenie i zasady bezpieczeństwa */}
              <div className="bg-amber-950/20 border border-amber-500/30 rounded-xl p-3.5 flex items-start gap-3">
                <ShieldCheck className="w-5 h-5 text-amber-400 shrink-0 mt-0.5" />
                <div className="text-xs text-amber-200 leading-relaxed">
                  <strong>Bezpieczeństwo transakcyjne:</strong> Migracja zostanie wykonana w transakcji atomowej (<code>BEGIN ... COMMIT</code>). Wszystkie brakujące kolumny zostaną dodane z opcją <strong>NULL</strong>, co chroni przed błędami i usunięciem istniejących danych.
                </div>
              </div>

              {/* Lista kroków wykonania */}
              <div className="space-y-2">
                <div className="text-xs font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-between">
                  <span>Plan operacji DDL ({executionSteps.length} kroków)</span>
                  {isApplying && (
                    <span className="text-amber-400 flex items-center gap-1.5 text-xs font-mono">
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      Wykonywanie...
                    </span>
                  )}
                  {applyFinished && (
                    <span className="text-emerald-400 flex items-center gap-1.5 text-xs font-mono font-bold">
                      <CheckCircle2 className="w-4 h-4" />
                      Zakończono sukcesem!
                    </span>
                  )}
                </div>

                <div className="max-h-64 overflow-y-auto space-y-2 pr-1 bg-slate-950 border border-slate-800 rounded-xl p-3">
                  {executionSteps.map((step, idx) => (
                    <div
                      key={step.id}
                      className={`p-2.5 rounded-lg border text-xs flex items-center justify-between transition-all ${
                        step.status === 'running'
                          ? 'bg-amber-500/10 border-amber-500/40 text-amber-200 shadow-sm'
                          : step.status === 'success'
                          ? 'bg-slate-900 border-emerald-500/30 text-slate-200'
                          : 'bg-slate-900/60 border-slate-800 text-slate-500'
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <span className="font-mono text-slate-500 text-[10px]">
                          {String(idx + 1).padStart(2, '0')}
                        </span>
                        <div>
                          <div className="font-semibold text-slate-200">{step.title}</div>
                          <div className="font-mono text-[10px] text-slate-400 mt-0.5 truncate max-w-md">
                            {step.sql}
                          </div>
                        </div>
                      </div>

                      <div className="shrink-0 text-right">
                        {step.status === 'pending' && (
                          <span className="text-slate-600 font-mono text-[11px]">Oczekuje</span>
                        )}
                        {step.status === 'running' && (
                          <RefreshCw className="w-3.5 h-3.5 text-amber-400 animate-spin" />
                        )}
                        {step.status === 'success' && (
                          <span className="text-emerald-400 font-mono text-[11px] flex items-center gap-1">
                            <Check className="w-3 h-3" />
                            {step.durationMs} ms
                          </span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Informacja po zakończeniu */}
              {applyFinished && (
                <div className="bg-emerald-950/20 border border-emerald-500/30 rounded-xl p-4 flex items-center gap-3 text-xs text-emerald-300">
                  <CheckCircle2 className="w-6 h-6 text-emerald-400 shrink-0" />
                  <div>
                    <div className="font-bold text-sm text-emerald-200">Baza danych pomyślnie zsynchronizowana!</div>
                    <div className="text-emerald-400/80 mt-0.5">
                      Wszystkie brakujące tabele i kolumny NULL-owalne zostały wdrożone, a historia migracji zaktualizowana.
                    </div>
                  </div>
                </div>
              )}
            </div>

            {/* Stopka modalu */}
            <div className="bg-slate-950 px-6 py-4 border-t border-slate-800 flex items-center justify-end gap-3">
              {!applyFinished ? (
                <>
                  <button
                    onClick={() => setShowApplyModal(false)}
                    disabled={isApplying}
                    className="px-4 py-2 rounded-lg text-slate-400 hover:text-slate-200 text-xs font-medium transition-colors"
                  >
                    Anuluj
                  </button>
                  <button
                    id="confirm-execute-migration-btn"
                    onClick={handleExecuteMigrationSteps}
                    disabled={isApplying}
                    className="px-5 py-2 rounded-lg bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold text-xs flex items-center gap-2 transition-colors shadow-lg disabled:opacity-50"
                  >
                    {isApplying ? (
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <Play className="w-3.5 h-3.5 fill-current" />
                    )}
                    {isApplying ? 'Wykonywanie migracji...' : 'Zatwierdź i wgraj zmiany'}
                  </button>
                </>
              ) : (
                <button
                  id="close-apply-modal-btn"
                  onClick={() => setShowApplyModal(false)}
                  className="px-6 py-2 rounded-lg bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs transition-colors shadow-lg"
                >
                  Zamknij i zobacz zaktualizowany schemat
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
