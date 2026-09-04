import React, { useState } from 'react';
import JSZip from 'jszip';
import { 
  Download, 
  FolderArchive, 
  FileCode, 
  Database, 
  ShieldCheck, 
  Check, 
  Copy, 
  Terminal, 
  Laptop, 
  Key, 
  Users, 
  ExternalLink,
  Layers,
  Sparkles,
  Server,
  Radio,
  Activity
} from 'lucide-react';
import { PROJECT_FILES, ProjectFile } from './data/projectFiles';
import { OidcFlowTester } from './components/OidcFlowTester/OidcFlowTester';
import { TelemetryDashboard } from './components/Telemetry/TelemetryDashboard';
import { SqlMigrationsTab } from './components/Migrations/SqlMigrationsTab';

export default function App() {
  const [selectedFile, setSelectedFile] = useState<ProjectFile>(PROJECT_FILES[0]);
  const [activeTab, setActiveTab] = useState<'explorer' | 'database' | 'migrations' | 'architecture' | 'oidc-tester' | 'telemetry' | 'guide'>(() => {
    if (typeof window !== 'undefined') {
      const hash = window.location.hash.replace('#', '');
      if (['explorer', 'database', 'migrations', 'architecture', 'oidc-tester', 'telemetry', 'guide'].includes(hash)) {
        return hash as any;
      }
    }
    return 'migrations';
  });
  const [copied, setCopied] = useState(false);
  const [isZipping, setIsZipping] = useState(false);
  const [zipSuccess, setZipSuccess] = useState(false);
  const [dbProvider, setDbProvider] = useState<'Sqlite' | 'PostgreSQL'>('Sqlite');

  // Funkcja generowania i pobierania archiwum ZIP
  const handleDownloadZip = async () => {
    try {
      setIsZipping(true);
      const zip = new JSZip();

      // Dodajemy wszystkie pliki projektu do archiwum ZIP
      PROJECT_FILES.forEach((file) => {
        zip.file(file.path, file.content);
      });

      // Generujemy blob zipa
      const content = await zip.generateAsync({ type: 'blob' });
      const url = window.URL.createObjectURL(content);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'Quorum.NET10.zip';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      setZipSuccess(true);
      setTimeout(() => setZipSuccess(false), 4000);
    } catch (err) {
      console.error('Błąd podczas pakowania ZIP:', err);
    } finally {
      setIsZipping(false);
    }
  };

  const handleCopyCode = () => {
    navigator.clipboard.writeText(selectedFile.content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-blue-600 selection:text-white">
      {/* Top Navigation Bar */}
      <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-blue-600 flex items-center justify-center text-white shadow-lg shadow-blue-600/30">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-base font-bold text-white tracking-tight">Quorum (Quorum.Backend)</h1>
                <span className="px-2 py-0.5 text-xs font-semibold bg-blue-500/10 text-blue-400 border border-blue-500/20 rounded-full">
                  .NET 10
                </span>
              </div>
              <p className="text-xs text-slate-400">Open.IdentityServer + EF Core + AspNetIdentity + Bootstrap 5 Razor CRUD</p>
            </div>
          </div>

          {/* Quick ZIP Export Action */}
          <div className="flex items-center gap-3">
            <button
              onClick={handleDownloadZip}
              disabled={isZipping}
              className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white text-sm font-medium rounded-lg shadow-md hover:shadow-blue-500/20 transition-all cursor-pointer disabled:opacity-50"
            >
              {isZipping ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  <span>Pakowanie ZIP...</span>
                </>
              ) : zipSuccess ? (
                <>
                  <Check className="w-4 h-4 text-emerald-300" />
                  <span>Pobrano pomyślnie!</span>
                </>
              ) : (
                <>
                  <Download className="w-4 h-4" />
                  <span>Pobierz ZIP (.NET 10)</span>
                </>
              )}
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 flex-1 flex flex-col gap-6 w-full">
        {/* Banner with Key Features */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 shadow-sm">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div>
              <div className="flex items-center gap-2 text-blue-400 text-xs font-semibold uppercase tracking-wider mb-1">
                <Sparkles className="w-4 h-4" /> Gotowy szablon startowy
              </div>
              <h2 className="text-xl font-bold text-white">Projekt .NET 10 ze zintegrowanym magazynem danych</h2>
              <p className="text-sm text-slate-400 mt-1 max-w-3xl">
                Wszystkie pliki projektu zostały wygenerowane i przygotowane do pobrania w archiwum ZIP. Rozwiązanie zawiera gotową obsługę wymiennych baz danych (SQLite / PostgreSQL), lokalnych kont użytkowników oraz kompletny panel CRUD w Razor Pages.
              </p>
            </div>

            <div className="flex flex-wrap gap-2">
              <div className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800/80 border border-slate-700/50 rounded-lg text-xs text-slate-300">
                <Database className="w-3.5 h-3.5 text-emerald-400" /> SQLite & PostgreSQL
              </div>
              <div className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800/80 border border-slate-700/50 rounded-lg text-xs text-slate-300">
                <Users className="w-3.5 h-3.5 text-blue-400" /> AspNetIdentity
              </div>
              <div className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800/80 border border-slate-700/50 rounded-lg text-xs text-slate-300">
                <Laptop className="w-3.5 h-3.5 text-purple-400" /> Razor Pages + Bootstrap 5
              </div>
              <div className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-500/10 border border-emerald-500/30 rounded-lg text-xs text-emerald-300">
                <Radio className="w-3.5 h-3.5 text-emerald-400" /> OIDC Flow Tester (PKCE & M2M)
              </div>
            </div>
          </div>

          {/* Navigation Tabs */}
          <div className="flex gap-2 border-t border-slate-800/80 mt-5 pt-4">
            <button
              onClick={() => setActiveTab('explorer')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'explorer'
                  ? 'bg-blue-600/15 text-blue-400 border border-blue-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <FileCode className="w-4 h-4" /> Eksplorator Plików Kodu
            </button>
            <button
              onClick={() => setActiveTab('database')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'database'
                  ? 'bg-blue-600/15 text-blue-400 border border-blue-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Database className="w-4 h-4" /> Konfigurator Bazy Danych
            </button>
            <button
              id="tab-migrations-btn"
              onClick={() => setActiveTab('migrations')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'migrations'
                  ? 'bg-amber-500/15 text-amber-400 border border-amber-500/40 shadow-sm'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Database className="w-4 h-4 text-amber-400" />
              <span>Migracje SQL</span>
              <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-amber-500/20 text-amber-300 rounded-full border border-amber-500/30">
                Idempotent & Compare
              </span>
            </button>
            <button
              onClick={() => setActiveTab('architecture')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'architecture'
                  ? 'bg-blue-600/15 text-blue-400 border border-blue-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Layers className="w-4 h-4" /> Architektura i OIDC Endpoints
            </button>
            <button
              onClick={() => setActiveTab('oidc-tester')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'oidc-tester'
                  ? 'bg-emerald-600/15 text-emerald-400 border border-emerald-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Radio className="w-4 h-4 text-emerald-400" />
              <span>Tester Przepływów OIDC</span>
              <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-emerald-500/20 text-emerald-300 rounded-full border border-emerald-500/30">
                Flow Tester
              </span>
            </button>
            <button
              onClick={() => setActiveTab('telemetry')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'telemetry'
                  ? 'bg-emerald-600/15 text-emerald-400 border border-emerald-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Activity className="w-4 h-4 text-emerald-400" />
              <span>Telemetria & Metryki</span>
              <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-emerald-500/20 text-emerald-300 rounded-full border border-emerald-500/30">
                OpenTelemetry
              </span>
            </button>
            <button
              onClick={() => setActiveTab('guide')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors cursor-pointer ${
                activeTab === 'guide'
                  ? 'bg-blue-600/15 text-blue-400 border border-blue-500/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`}
            >
              <Terminal className="w-4 h-4" /> Instrukcja Uruchomienia
            </button>
          </div>
        </div>

        {/* Tab 1: File Explorer & Code Viewer */}
        {activeTab === 'explorer' && (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
            {/* File List Tree */}
            <div className="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-2xl p-4 flex flex-col gap-2">
              <div className="flex items-center justify-between px-2 pb-2 border-b border-slate-800">
                <div className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                  <FolderArchive className="w-4 h-4 text-blue-400" /> Pliki Rozwiązania ({PROJECT_FILES.length})
                </div>
                <span className="text-xs text-slate-500">.NET 10</span>
              </div>

              <div className="flex flex-col gap-1 max-h-[580px] overflow-y-auto pr-1">
                {PROJECT_FILES.map((file) => {
                  const isSelected = selectedFile.path === file.path;
                  return (
                    <button
                      key={file.path}
                      onClick={() => setSelectedFile(file)}
                      className={`w-full text-left px-3 py-2.5 rounded-xl text-xs transition-all flex items-start gap-2.5 cursor-pointer ${
                        isSelected
                          ? 'bg-blue-600 text-white font-medium shadow-md shadow-blue-600/20'
                          : 'text-slate-300 hover:bg-slate-800/80 hover:text-white'
                      }`}
                    >
                      <FileCode className={`w-4 h-4 mt-0.5 shrink-0 ${isSelected ? 'text-white' : 'text-slate-400'}`} />
                      <div className="truncate">
                        <div className="font-mono truncate">{file.name}</div>
                        <div className={`text-[10px] truncate ${isSelected ? 'text-blue-100' : 'text-slate-400'}`}>
                          {file.description}
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Code Preview Viewer */}
            <div className="lg:col-span-8 bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden flex flex-col">
              <div className="bg-slate-950 px-4 py-3 border-b border-slate-800 flex items-center justify-between">
                <div>
                  <div className="font-mono text-xs text-blue-400">{selectedFile.path}</div>
                  <div className="text-xs text-slate-400 mt-0.5">{selectedFile.description}</div>
                </div>
                <button
                  onClick={handleCopyCode}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium rounded-lg border border-slate-700 transition-colors cursor-pointer"
                >
                  {copied ? (
                    <>
                      <Check className="w-3.5 h-3.5 text-emerald-400" />
                      <span>Skopiowano</span>
                    </>
                  ) : (
                    <>
                      <Copy className="w-3.5 h-3.5" />
                      <span>Kopiuj kod</span>
                    </>
                  )}
                </button>
              </div>

              <div className="p-4 overflow-x-auto max-h-[580px] bg-slate-950/70">
                <pre className="text-xs font-mono text-slate-200 leading-relaxed">
                  <code>{selectedFile.content}</code>
                </pre>
              </div>
            </div>
          </div>
        )}

        {/* Tab 2: Database Configurator (SQLite ↔ PostgreSQL) */}
        {activeTab === 'database' && (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <div className="lg:col-span-5 bg-slate-900 border border-slate-800 rounded-2xl p-6 flex flex-col gap-5">
              <div>
                <h3 className="text-base font-bold text-white flex items-center gap-2">
                  <Database className="w-5 h-5 text-emerald-400" /> Wybór Silnika Bazy Danych
                </h3>
                <p className="text-xs text-slate-400 mt-1">
                  Dzięki metodzie rozszerzającej <code className="text-blue-400">ConfigureDatabase</code> przełączenie bazy sprowadza się do jednej linijki w konfiguracji.
                </p>
              </div>

              <div className="flex flex-col gap-3">
                <label
                  onClick={() => setDbProvider('Sqlite')}
                  className={`p-4 rounded-xl border transition-all cursor-pointer flex items-start gap-3 ${
                    dbProvider === 'Sqlite'
                      ? 'bg-blue-600/10 border-blue-500/50 text-white'
                      : 'bg-slate-800/40 border-slate-700/50 text-slate-400 hover:bg-slate-800/80'
                  }`}
                >
                  <input
                    type="radio"
                    name="dbProvider"
                    checked={dbProvider === 'Sqlite'}
                    onChange={() => setDbProvider('Sqlite')}
                    className="mt-1"
                  />
                  <div>
                    <div className="font-semibold text-sm text-slate-200">SQLite (Domyślny dla developmentu)</div>
                    <div className="text-xs text-slate-400 mt-0.5">
                      Baza w jednym lokalnym pliku <code className="text-slate-300">identityserver.db</code>. Nie wymaga instalowania zewnętrznych serwerów bazy.
                    </div>
                  </div>
                </label>

                <label
                  onClick={() => setDbProvider('PostgreSQL')}
                  className={`p-4 rounded-xl border transition-all cursor-pointer flex items-start gap-3 ${
                    dbProvider === 'PostgreSQL'
                      ? 'bg-blue-600/10 border-blue-500/50 text-white'
                      : 'bg-slate-800/40 border-slate-700/50 text-slate-400 hover:bg-slate-800/80'
                  }`}
                >
                  <input
                    type="radio"
                    name="dbProvider"
                    checked={dbProvider === 'PostgreSQL'}
                    onChange={() => setDbProvider('PostgreSQL')}
                    className="mt-1"
                  />
                  <div>
                    <div className="font-semibold text-sm text-slate-200">PostgreSQL (Dla mikrousług i produkcji)</div>
                    <div className="text-xs text-slate-400 mt-0.5">
                      Wydajny, transakcyjny serwer bazodanowy z Npgsql EF Core Provider. Idealny dla klastrów Docker i Kubernetes.
                    </div>
                  </div>
                </label>
              </div>

              <div className="p-4 bg-slate-950/80 border border-slate-800 rounded-xl">
                <div className="text-xs font-semibold text-slate-300 mb-1">Docker Compose dla PostgreSQL:</div>
                <pre className="text-[11px] font-mono text-emerald-400 leading-tight">
{`docker run -d \\
  --name identity-postgres \\
  -e POSTGRES_DB=identity_server_db \\
  -e POSTGRES_PASSWORD=postgres \\
  -p 5432:5432 \\
  postgres:16-alpine`}
                </pre>
              </div>
            </div>

            <div className="lg:col-span-7 bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden flex flex-col">
              <div className="bg-slate-950 px-4 py-3 border-b border-slate-800 flex items-center justify-between">
                <div className="text-xs font-mono text-blue-400">appsettings.json ({dbProvider})</div>
                <span className="text-xs text-emerald-400 font-medium">Aktywny dostawca: {dbProvider}</span>
              </div>
              <div className="p-4 bg-slate-950/70 overflow-x-auto">
                <pre className="text-xs font-mono text-slate-200">
{`{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Open.IdentityServer": "Information"
    }
  },
  "DatabaseProvider": "${dbProvider}",
  "ConnectionStrings": {
    "Sqlite": "Data Source=identityserver.db",
    "PostgreSQL": "Host=localhost;Port=5432;Database=identity_server_db;Username=postgres;Password=postgres;"
  }
}`}
                </pre>
              </div>
              <div className="p-4 bg-slate-900 border-t border-slate-800 text-xs text-slate-400">
                <p>
                  Metoda <code className="text-blue-400 font-mono">SeedData.EnsureSeedDataAsync()</code> automatycznie wywołuje <code className="text-slate-300 font-mono">EnsureCreatedAsync()</code> dla wszystkich trzech kontekstów (ApplicationDbContext, ConfigurationDbContext, PersistedGrantDbContext).
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Tab 3: Architecture & OIDC Endpoints */}
        {activeTab === 'architecture' && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col gap-3">
              <div className="w-8 h-8 rounded-lg bg-blue-500/10 text-blue-400 flex items-center justify-center">
                <Server className="w-4 h-4" />
              </div>
              <h4 className="font-bold text-sm text-white">OpenID Discovery & Klucze</h4>
              <p className="text-xs text-slate-400">
                Standardowy punkt końcowy OIDC zwracający metadane serwera tożsamości:
              </p>
              <div className="bg-slate-950 p-2.5 rounded-lg font-mono text-[11px] text-blue-300 break-all">
                GET /.well-known/openid-configuration
              </div>
              <div className="bg-slate-950 p-2.5 rounded-lg font-mono text-[11px] text-slate-400 break-all">
                GET /.well-known/openid-configuration/jwks
              </div>
            </div>

            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col gap-3">
              <div className="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                <Key className="w-4 h-4" />
              </div>
              <h4 className="font-bold text-sm text-white">Endpoint Wydawania Tokenów</h4>
              <p className="text-xs text-slate-400">
                Obsługuje przepływy <code className="text-slate-300">authorization_code</code>, <code className="text-slate-300">client_credentials</code> oraz <code className="text-slate-300">refresh_token</code>:
              </p>
              <div className="bg-slate-950 p-2.5 rounded-lg font-mono text-[11px] text-emerald-300 break-all">
                POST /connect/token
              </div>
              <div className="bg-slate-950 p-2.5 rounded-lg font-mono text-[11px] text-slate-400 break-all">
                GET/POST /connect/authorize
              </div>
              <button
                onClick={() => setActiveTab('oidc-tester')}
                className="mt-1 flex items-center justify-center gap-1.5 px-3 py-1.5 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded-lg text-xs font-medium transition-colors cursor-pointer"
              >
                <Radio className="w-3.5 h-3.5" />
                <span>Otwórz w Testerze Przepływów OIDC</span>
              </button>
            </div>

            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col gap-3">
              <div className="w-8 h-8 rounded-lg bg-purple-500/10 text-purple-400 flex items-center justify-center">
                <Users className="w-4 h-4" />
              </div>
              <h4 className="font-bold text-sm text-white">Zarządzanie GUI (Razor Pages)</h4>
              <p className="text-xs text-slate-400">
                Pełny serwerowy CRUD z autoryzacją ról:
              </p>
              <ul className="text-xs text-slate-300 space-y-1">
                <li>• <code className="text-blue-400 font-mono">/Admin/Clients</code> (Klienci OIDC)</li>
                <li>• <code className="text-blue-400 font-mono">/Admin/ApiScopes</code> (Zakresy API)</li>
                <li>• <code className="text-blue-400 font-mono">/Admin/Users</code> (Konta AspNetIdentity)</li>
                <li>• <code className="text-blue-400 font-mono">/Admin/Grants</code> (Aktywne sesje i tokeny)</li>
              </ul>
            </div>
          </div>
        )}

        {/* Tab: SQL Migrations & Schema Compare */}
        {activeTab === 'migrations' && (
          <SqlMigrationsTab />
        )}

        {/* Tab: OIDC Flow Tester */}
        {activeTab === 'oidc-tester' && (
          <OidcFlowTester />
        )}

        {/* Tab: OpenTelemetry Dashboard */}
        {activeTab === 'telemetry' && (
          <TelemetryDashboard />
        )}

        {/* Tab 4: Step-by-Step Local Launch Guide */}
        {activeTab === 'guide' && (
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 flex flex-col gap-6">
            <div>
              <h3 className="text-lg font-bold text-white">Instrukcja Uruchomienia Projektu Krok po Kroku</h3>
              <p className="text-xs text-slate-400 mt-1">
                Poniżej znajdują się polecenia potrzebne do uruchomienia pobranego rozwiązania .NET 10 na Twoim komputerze.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col gap-2">
                <div className="flex items-center gap-2 text-sm font-semibold text-blue-400">
                  <span className="w-6 h-6 rounded-full bg-blue-600/20 text-blue-400 flex items-center justify-center text-xs">1</span>
                  Pobierz i rozpakuj ZIP
                </div>
                <p className="text-xs text-slate-400">
                  Kliknij przycisk <strong>„Pobierz ZIP (.NET 10)”</strong> na górnym pasku i rozpakuj archiwum do wybranego katalogu.
                </p>
              </div>

              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col gap-2">
                <div className="flex items-center gap-2 text-sm font-semibold text-blue-400">
                  <span className="w-6 h-6 rounded-full bg-blue-600/20 text-blue-400 flex items-center justify-center text-xs">2</span>
                  Uruchom aplikację
                </div>
                <pre className="text-[11px] font-mono text-emerald-400 bg-slate-900 p-2 rounded border border-slate-800">
{`# Możesz uruchomić bezpośrednio nową solucję XML lub projekt
dotnet restore Quorum.slnx
dotnet run --project Quorum.Backend`}
                </pre>
              </div>

              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col gap-2">
                <div className="flex items-center gap-2 text-sm font-semibold text-blue-400">
                  <span className="w-6 h-6 rounded-full bg-blue-600/20 text-blue-400 flex items-center justify-center text-xs">3</span>
                  Zaloguj się do Admin GUI
                </div>
                <div className="text-xs text-slate-300 space-y-1">
                  <div>Adres: <code className="text-blue-400">https://localhost:5001/Admin</code></div>
                  <div>Login: <code className="text-white">admin</code></div>
                  <div>Hasło: <code className="text-white">Pass123$</code></div>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800 bg-slate-950 py-4 text-center text-xs text-slate-400">
        Quorum.slnx • Quorum.Backend (.NET 10) • Open.IdentityServer • SQLite & PostgreSQL EF Core • Razor Pages CRUD
      </footer>
    </div>
  );
}
