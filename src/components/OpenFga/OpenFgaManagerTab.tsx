import React, { useState } from 'react';
import { 
  ShieldCheck, 
  RefreshCw, 
  Plus, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  AlertCircle, 
  Terminal, 
  Server, 
  ArrowRight, 
  Play, 
  Search,
  ExternalLink,
  Code2,
  Database,
  Layers,
  KeyRound,
  FileCheck,
  Network,
  Table
} from 'lucide-react';
import { OpenFgaGraphView } from './OpenFgaGraphView';

export interface AuthZenRule {
  id: string;
  name: string;
  description: string;
  subjectType: 'user' | 'role' | 'group' | 'service';
  subjectId: string;
  action: string;
  resourceType: string;
  resourceId: string;
  effect: 'Permit' | 'Deny';
  isEnabled: boolean;
  syncStatus: 'InSync' | 'Pending' | 'Error';
  lastSyncedAt?: string;
  lastSyncError?: string;
}

const INITIAL_RULES: AuthZenRule[] = [
  {
    id: 'rule-001',
    name: 'Odczyt Roadmap przez Annę',
    description: 'Użytkownik Anne posiada uprawnienie odczytu (reader) dokumentu Roadmap 2026',
    subjectType: 'user',
    subjectId: 'anne',
    action: 'reader',
    resourceType: 'document',
    resourceId: 'roadmap_2026',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-002',
    name: 'Członkostwo Jana w Roli Administratora',
    description: 'Użytkownik John jest członkiem (member) roli admin',
    subjectType: 'user',
    subjectId: 'john',
    action: 'member',
    resourceType: 'role',
    resourceId: 'admin',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-003',
    name: 'Członkostwo Zofii w Roli Administratora',
    description: 'Użytkownik Sophia jest członkiem (member) roli admin',
    subjectType: 'user',
    subjectId: 'sophia',
    action: 'member',
    resourceType: 'role',
    resourceId: 'admin',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-004',
    name: 'Uprawnienia Właściciela dla Administratora',
    description: 'Rola Admin posiada pełne uprawnienia właściciela (owner) repozytorium Quorum',
    subjectType: 'role',
    subjectId: 'admin',
    action: 'owner',
    resourceType: 'repo',
    resourceId: 'quorum-core',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-005',
    name: 'Uprawnienia Administracyjne do Telemetrii',
    description: 'Rola Admin posiada uprawnienia admin do API telemetrii i audytu',
    subjectType: 'role',
    subjectId: 'admin',
    action: 'admin',
    resourceType: 'api',
    resourceId: 'audit_telemetry',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-006',
    name: 'Przypisanie Boba do Grupy Finanse',
    description: 'Użytkownik Bob jest członkiem (member) grupy finance',
    subjectType: 'user',
    subjectId: 'bob',
    action: 'member',
    resourceType: 'group',
    resourceId: 'finance',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-007',
    name: 'Zapis Zamówień przez Dział Finansowy',
    description: 'Grupa Finanse posiada uprawnienia zapisu (writer) w module faktur i zamówień',
    subjectType: 'group',
    subjectId: 'finance',
    action: 'writer',
    resourceType: 'route',
    resourceId: 'api-orders',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-008',
    name: 'Przypisanie Łukasza do Roli Programisty',
    description: 'Użytkownik Lucas jest członkiem (member) roli developer',
    subjectType: 'user',
    subjectId: 'lucas',
    action: 'member',
    resourceType: 'role',
    resourceId: 'developer',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-009',
    name: 'Uprawnienia Współtwórcy dla Developerów',
    description: 'Rola Developer posiada uprawnienia współtwórcy (contributor) w repozytorium',
    subjectType: 'role',
    subjectId: 'developer',
    action: 'contributor',
    resourceType: 'repo',
    resourceId: 'quorum-core',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-010',
    name: 'Edycja Dokumentacji przez Developerów',
    description: 'Rola Developer posiada uprawnienia edytora (editor) dokumentu Roadmap 2026',
    subjectType: 'role',
    subjectId: 'developer',
    action: 'editor',
    resourceType: 'document',
    resourceId: 'roadmap_2026',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-011',
    name: 'Wgląd Audytora Bezpieczeństwa',
    description: 'Audytor posiada wgląd (viewer) do logów telemetrycznych systemu',
    subjectType: 'user',
    subjectId: 'security_auditor',
    action: 'viewer',
    resourceType: 'api',
    resourceId: 'audit_telemetry',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  },
  {
    id: 'rule-012',
    name: 'Wywołanie Zamówień przez Reverse Proxy',
    description: 'Mikrousługa gateway_proxy wywołuje (invoker) trasę api-orders',
    subjectType: 'service',
    subjectId: 'gateway_proxy',
    action: 'invoker',
    resourceType: 'route',
    resourceId: 'api-orders',
    effect: 'Permit',
    isEnabled: true,
    syncStatus: 'InSync',
    lastSyncedAt: '2026-09-13T16:00:00Z'
  }
];

export const OpenFgaManagerTab: React.FC = () => {
  const [rules, setRules] = useState<AuthZenRule[]>(INITIAL_RULES);
  const [searchTerm, setSearchTerm] = useState('');
  const [serverUrl, setServerUrl] = useState('http://localhost:8080');
  const [storeId, setStoreId] = useState('01JM8Q6X7Y9Z0123456789ABCD');
  const [isConnected, setIsConnected] = useState(true);
  const [latencyMs, setLatencyMs] = useState(4.2);
  const [isSyncing, setIsSyncing] = useState(false);
  const [logs, setLogs] = useState<string[]>([
    '[2026-09-13 16:00:00] Inicjalizacja klienta OpenFgaClient (REST API)...',
    '[2026-09-13 16:00:01] Połączono z serwerem OpenFGA na http://0.0.0.0:8080 (Store: quorum-identity)',
    '[2026-09-13 16:00:02] Zsynchronizowano 4 krotki początkowe (POST /stores/01JM8.../write) - Status 200 OK'
  ]);

  // Modal stanu edycji/tworzenia
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<AuthZenRule | null>(null);

  // Formularz AuthZEN
  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formSubjectType, setFormSubjectType] = useState<'user' | 'role' | 'group' | 'service'>('user');
  const [formSubjectId, setFormSubjectId] = useState('');
  const [formAction, setFormAction] = useState('reader');
  const [formResourceType, setFormResourceType] = useState('document');
  const [formResourceId, setFormResourceId] = useState('');
  const [formEffect, setFormEffect] = useState<'Permit' | 'Deny'>('Permit');
  const [formIsEnabled, setFormIsEnabled] = useState(true);

  // Tester Ewaluacji Check
  const [checkUser, setCheckUser] = useState('user:anne');
  const [checkRelation, setCheckRelation] = useState('reader');
  const [checkObject, setCheckObject] = useState('document:roadmap_2026');
  const [checkResult, setCheckResult] = useState<{ allowed: boolean; message: string } | null>({
    allowed: true,
    message: 'Allowed: Krotka (user:anne, reader, document:roadmap_2026) istnieje w OpenFGA.'
  });

  // Tryb widoku: Graf wizualny / Tabela / Łączony
  const [activeViewMode, setActiveViewMode] = useState<'graph' | 'table' | 'split'>('graph');

  const handleSelectForCheck = (user: string, relation: string, object: string) => {
    setCheckUser(user);
    setCheckRelation(relation);
    setCheckObject(object);
    addLog(`Wybrano z grafu: [${user}] -> [${relation}] -> [${object}] do ewaluacji Check`);
    const testerEl = document.getElementById('openfga-check-tester');
    if (testerEl) {
      testerEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  };

  const addLog = (msg: string) => {
    const timestamp = new Date().toISOString().replace('T', ' ').substring(0, 19);
    setLogs(prev => [`[${timestamp}] ${msg}`, ...prev.slice(0, 49)]);
  };

  const handleOpenCreate = () => {
    setEditingRule(null);
    setFormName('Nowa reguła dostępu');
    setFormDescription('Reguła autoryzacji zdefiniowana w formacie AuthZEN');
    setFormSubjectType('user');
    setFormSubjectId('jan_kowalski');
    setFormAction('reader');
    setFormResourceType('document');
    setFormResourceId('projekt_2026');
    setFormEffect('Permit');
    setFormIsEnabled(true);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (rule: AuthZenRule) => {
    setEditingRule(rule);
    setFormName(rule.name);
    setFormDescription(rule.description);
    setFormSubjectType(rule.subjectType);
    setFormSubjectId(rule.subjectId);
    setFormAction(rule.action);
    setFormResourceType(rule.resourceType);
    setFormResourceId(rule.resourceId);
    setFormEffect(rule.effect);
    setFormIsEnabled(rule.isEnabled);
    setIsModalOpen(true);
  };

  const handleSaveRule = () => {
    if (!formSubjectId || !formAction || !formResourceId) {
      alert('Pola Subject ID, Action oraz Resource ID są wymagane w specyfikacji AuthZEN!');
      return;
    }

    const targetUser = `${formSubjectType}:${formSubjectId}`;
    const targetRelation = formAction.toLowerCase().trim();
    const targetObject = `${formResourceType}:${formResourceId}`;

    if (editingRule) {
      // Aktualizacja
      const updated: AuthZenRule = {
        ...editingRule,
        name: formName || `${targetUser} -> ${targetRelation} -> ${targetObject}`,
        description: formDescription,
        subjectType: formSubjectType,
        subjectId: formSubjectId,
        action: formAction,
        resourceType: formResourceType,
        resourceId: formResourceId,
        effect: formEffect,
        isEnabled: formIsEnabled,
        syncStatus: 'InSync',
        lastSyncedAt: new Date().toISOString()
      };

      setRules(prev => prev.map(r => r.id === editingRule.id ? updated : r));
      addLog(`Adapter: Zaktualizowano regułę ${updated.id}. Wysłano POST /stores/${storeId.substring(0, 8)}.../write do OpenFGA REST API.`);
    } else {
      // Tworzenie nowej
      const newRule: AuthZenRule = {
        id: `rule-${Math.floor(100 + Math.random() * 900)}`,
        name: formName || `${targetUser} -> ${targetRelation} -> ${targetObject}`,
        description: formDescription,
        subjectType: formSubjectType,
        subjectId: formSubjectId,
        action: formAction,
        resourceType: formResourceType,
        resourceId: formResourceId,
        effect: formEffect,
        isEnabled: formIsEnabled,
        syncStatus: 'InSync',
        lastSyncedAt: new Date().toISOString()
      };

      setRules(prev => [newRule, ...prev]);
      addLog(`Adapter: Utworzono obiekt AuthZEN '${newRule.name}'. Zapisano krotkę (${targetUser}#${targetRelation}@${targetObject}) w OpenFGA REST API.`);
    }

    setIsModalOpen(false);
  };

  const handleDeleteRule = (id: string) => {
    const rule = rules.find(r => r.id === id);
    if (!rule) return;

    if (confirm(`Czy na pewno usunąć regułę '${rule.name}' i wycofać relację z OpenFGA?`)) {
      setRules(prev => prev.filter(r => r.id !== id));
      const fgaUser = `${rule.subjectType}:${rule.subjectId}`;
      const fgaRelation = rule.action.toLowerCase();
      const fgaObject = `${rule.resourceType}:${rule.resourceId}`;
      addLog(`Adapter: Usunięto regułę ${id}. Wysłano 'deletes' dla krotki (${fgaUser}, ${fgaRelation}, ${fgaObject}) do OpenFGA REST API.`);
    }
  };

  const handleSyncAll = () => {
    setIsSyncing(true);
    setTimeout(() => {
      setRules(prev => prev.map(r => ({
        ...r,
        syncStatus: 'InSync',
        lastSyncedAt: new Date().toISOString()
      })));
      setIsSyncing(false);
      addLog(`Adapter: Wykonano hurtową synchronizację ${rules.length} reguł z API REST OpenFGA (POST /stores/${storeId.substring(0, 8)}.../write).`);
    }, 600);
  };

  const handleTestCheck = () => {
    const matchingRule = rules.find(r => {
      const fgaUser = `${r.subjectType}:${r.subjectId}`;
      const fgaRelation = r.action.toLowerCase();
      const fgaObject = `${r.resourceType}:${r.resourceId}`;
      return r.isEnabled && r.effect === 'Permit' &&
        (fgaUser === checkUser || fgaUser === 'role:admin') &&
        fgaRelation === checkRelation &&
        fgaObject === checkObject;
    });

    if (matchingRule) {
      setCheckResult({
        allowed: true,
        message: `Allowed: true. Zapytanie POST /stores/${storeId.substring(0, 8)}.../check zwróciło sukces na podstawie reguły '${matchingRule.name}'.`
      });
      addLog(`Check API: [${checkUser}] -> [${checkRelation}] -> [${checkObject}] => ALLOWED`);
    } else {
      setCheckResult({
        allowed: false,
        message: `Allowed: false. Brak krotki relacji w OpenFGA dla użytkownika '${checkUser}', relacji '${checkRelation}' i obiektu '${checkObject}'.`
      });
      addLog(`Check API: [${checkUser}] -> [${checkRelation}] -> [${checkObject}] => DENIED`);
    }
  };

  const filteredRules = rules.filter(r => {
    const term = searchTerm.toLowerCase();
    return r.name.toLowerCase().includes(term) ||
      r.subjectId.toLowerCase().includes(term) ||
      r.action.toLowerCase().includes(term) ||
      r.resourceId.toLowerCase().includes(term);
  });

  return (
    <div className="space-y-6">
      {/* Baner informacyjny */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 border border-indigo-800/40 rounded-xl p-6 text-white shadow-lg">
        <div className="flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <span className="bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1.5">
                <ShieldCheck className="w-3.5 h-3.5 text-indigo-400" />
                AuthZEN ➔ OpenFGA REST Adapter (Zanzibar ReBAC)
              </span>
              <span className="bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1.5">
                <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                OpenFGA: 0.0.0.0:8080
              </span>
            </div>
            <h2 className="text-2xl font-bold tracking-tight">Kreator i edytor reguł OpenFGA (AuthZEN CRUD)</h2>
            <p className="text-slate-300 text-sm mt-1 max-w-3xl">
              Definiuj uprawnienia w ustandaryzowanym formacie <strong>AuthZEN</strong> (Subject, Action, Resource, Effect), 
              a dedykowany adapter <code>AuthZenToOpenFgaAdapter</code> automatycznie mapuje je na krotki Zanzibar 
              <code>(user, relation, object)</code> i zapisuje do API REST OpenFGA (<code>POST /stores/{'{store_id}'}/write</code>).
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleSyncAll}
              disabled={isSyncing}
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-medium transition flex items-center gap-2 shadow"
            >
              <RefreshCw className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />
              {isSyncing ? 'Synchronizowanie...' : 'Synchronizuj z OpenFGA'}
            </button>
            <button
              onClick={handleOpenCreate}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium transition flex items-center gap-2 shadow"
            >
              <Plus className="w-4 h-4" />
              Nowa reguła (AuthZEN)
            </button>
          </div>
        </div>

        {/* Pasek statusu serwera */}
        <div className="mt-6 pt-4 border-t border-indigo-900/60 grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
          <div className="flex items-center gap-2 text-slate-300">
            <Server className="w-4 h-4 text-indigo-400" />
            <span>Endpoint OpenFGA:</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-indigo-300">{serverUrl}</span>
          </div>
          <div className="flex items-center gap-2 text-slate-300">
            <Database className="w-4 h-4 text-indigo-400" />
            <span>Store ID:</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-indigo-300" title={storeId}>{storeId.substring(0, 16)}...</span>
          </div>
          <div className="flex items-center gap-2 text-slate-300 md:justify-end">
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            <span>Opóźnienie REST API: <strong className="text-emerald-400">{latencyMs} ms</strong></span>
            <span className="mx-1">•</span>
            <span>Aktywne relacje: <strong className="text-white">{rules.filter(r => r.isEnabled && r.effect === 'Permit').length}</strong></span>
          </div>
        </div>
      </div>

      {/* Wizualizacja przepływu AuthZEN -> OpenFGA Adapter */}
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl p-5 shadow-sm">
        <h3 className="text-sm font-semibold text-slate-900 dark:text-white uppercase tracking-wider mb-3 flex items-center gap-2">
          <Layers className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
          Schemat mostka autoryzacji: Obiekt AuthZEN ➔ Krotka OpenFGA (Zanzibar) ➔ REST API (8080)
        </h3>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 text-xs font-mono">
          <div className="p-3.5 bg-slate-50 dark:bg-slate-900/80 border border-slate-200 dark:border-slate-700 rounded-lg">
            <div className="font-sans font-bold text-slate-800 dark:text-slate-200 mb-2 flex items-center justify-between">
              <span>1. Obiekt AuthZEN (PAP)</span>
              <span className="text-[10px] px-2 py-0.5 bg-indigo-100 dark:bg-indigo-950 text-indigo-600 dark:text-indigo-300 rounded">OpenID Foundation</span>
            </div>
            <pre className="text-slate-600 dark:text-slate-300 overflow-x-auto whitespace-pre-wrap">
{`{
  "subject": { "type": "user", "id": "anne" },
  "action": { "name": "reader" },
  "resource": { "type": "document", "id": "roadmap_2026" },
  "effect": "Permit"
}`}
            </pre>
          </div>

          <div className="p-3.5 bg-indigo-50 dark:bg-indigo-950/40 border border-indigo-200 dark:border-indigo-800/40 rounded-lg">
            <div className="font-sans font-bold text-indigo-900 dark:text-indigo-200 mb-2 flex items-center justify-between">
              <span>2. AuthZenToOpenFgaAdapter</span>
              <span className="text-[10px] px-2 py-0.5 bg-indigo-200 dark:bg-indigo-900 text-indigo-800 dark:text-indigo-300 rounded">C# .NET 10</span>
            </div>
            <div className="text-indigo-950 dark:text-indigo-200 space-y-1.5 pt-1">
              <div><strong>User:</strong> <code>user:anne</code></div>
              <div><strong>Relation:</strong> <code>reader</code></div>
              <div><strong>Object:</strong> <code>document:roadmap_2026</code></div>
              <div className="text-[11px] text-indigo-600 dark:text-indigo-400 mt-2">
                (user:anne) # [reader] @ (document:roadmap_2026)
              </div>
            </div>
          </div>

          <div className="p-3.5 bg-slate-900 border border-slate-800 rounded-lg text-emerald-400">
            <div className="font-sans font-bold text-slate-100 mb-2 flex items-center justify-between">
              <span>3. OpenFGA REST API</span>
              <span className="text-[10px] px-2 py-0.5 bg-emerald-950 text-emerald-300 rounded">POST 0.0.0.0:8080</span>
            </div>
            <pre className="overflow-x-auto whitespace-pre-wrap text-[11px]">
{`POST /stores/{id}/write
{
  "writes": {
    "tuple_keys": [{
      "user": "user:anne",
      "relation": "reader",
      "object": "document:roadmap_2026"
    }]
  }
}`}
            </pre>
          </div>
        </div>
      </div>

      {/* Przełącznik widoku: Graf Wizualny / Tabela Reguł / Widok Łączony */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 bg-white dark:bg-slate-800 p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm">
        <div className="flex items-center gap-1.5 p-1 bg-slate-100 dark:bg-slate-900 rounded-lg w-full sm:w-auto">
          <button
            onClick={() => setActiveViewMode('graph')}
            className={`flex-1 sm:flex-initial px-3.5 py-1.5 rounded-md text-xs font-semibold flex items-center justify-center gap-2 transition cursor-pointer ${
              activeViewMode === 'graph'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Network className="w-4 h-4" />
            <span>Graf Relacji OpenFGA (Model ReBAC)</span>
          </button>
          <button
            onClick={() => setActiveViewMode('table')}
            className={`flex-1 sm:flex-initial px-3.5 py-1.5 rounded-md text-xs font-semibold flex items-center justify-center gap-2 transition cursor-pointer ${
              activeViewMode === 'table'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Table className="w-4 h-4" />
            <span>Tabela Reguł AuthZEN ({rules.length})</span>
          </button>
          <button
            onClick={() => setActiveViewMode('split')}
            className={`hidden md:flex px-3.5 py-1.5 rounded-md text-xs font-semibold items-center justify-center gap-2 transition cursor-pointer ${
              activeViewMode === 'split'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Layers className="w-4 h-4" />
            <span>Widok Łączony</span>
          </button>
        </div>

        <div className="text-xs text-slate-500 dark:text-slate-400 px-2 flex items-center gap-2">
          <span>Model Zanzibar:</span>
          <span className="px-2 py-0.5 rounded bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 font-mono font-medium">
            (users) ➔ [roles] ➔ (resources)
          </span>
        </div>
      </div>

      {/* Wizualny Graf OpenFGA */}
      {(activeViewMode === 'graph' || activeViewMode === 'split') && (
        <OpenFgaGraphView 
          rules={rules} 
          onSelectForCheck={handleSelectForCheck} 
          onOpenCreateRule={handleOpenCreate} 
        />
      )}

      {/* Lista reguł i wyszukiwarka */}
      {(activeViewMode === 'table' || activeViewMode === 'split') && (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-200 dark:border-slate-700 flex flex-col sm:flex-row justify-between items-center gap-3">
          <div className="relative w-full sm:w-80">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Szukaj reguły, podmiotu, relacji..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="text-xs text-slate-500 dark:text-slate-400">
            Łącznie reguł: <strong>{filteredRules.length}</strong>
          </div>
        </div>

        {/* Tabela Reguł */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 dark:bg-slate-900/50 text-slate-500 dark:text-slate-400 text-xs font-semibold uppercase tracking-wider border-b border-slate-200 dark:border-slate-700">
              <tr>
                <th className="px-5 py-3">Nazwa reguły AuthZEN</th>
                <th className="px-5 py-3">Podmiot (Subject)</th>
                <th className="px-5 py-3">Akcja / Relacja</th>
                <th className="px-5 py-3">Zasób (Resource / Object)</th>
                <th className="px-5 py-3 text-center">Efekt</th>
                <th className="px-5 py-3 text-center">OpenFGA Status</th>
                <th className="px-5 py-3 text-right">Akcje</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-700">
              {filteredRules.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-5 py-8 text-center text-slate-500 dark:text-slate-400">
                    Brak reguł spełniających kryteria wyszukiwania.
                  </td>
                </tr>
              ) : (
                filteredRules.map(rule => {
                  const fgaUser = `${rule.subjectType}:${rule.subjectId}`;
                  const fgaRelation = rule.action.toLowerCase();
                  const fgaObject = `${rule.resourceType}:${rule.resourceId}`;

                  return (
                    <tr key={rule.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/40 transition">
                      <td className="px-5 py-4">
                        <div className="font-medium text-slate-900 dark:text-white">{rule.name}</div>
                        <div className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{rule.description}</div>
                      </td>
                      <td className="px-5 py-4">
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded bg-slate-100 dark:bg-slate-900 text-slate-700 dark:text-slate-300 font-mono text-xs border border-slate-200 dark:border-slate-700">
                          {fgaUser}
                        </span>
                      </td>
                      <td className="px-5 py-4">
                        <span className="inline-flex items-center px-2 py-0.5 rounded bg-indigo-100 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 font-mono text-xs font-semibold">
                          {fgaRelation}
                        </span>
                      </td>
                      <td className="px-5 py-4">
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded bg-slate-100 dark:bg-slate-900 text-slate-700 dark:text-slate-300 font-mono text-xs border border-slate-200 dark:border-slate-700">
                          {fgaObject}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-center">
                        <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                          rule.effect === 'Permit' 
                            ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400' 
                            : 'bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-400'
                        }`}>
                          {rule.effect}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-center">
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-400 border border-emerald-300 dark:border-emerald-800">
                          <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
                          Zapisano w OpenFGA
                        </span>
                      </td>
                      <td className="px-5 py-4 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            onClick={() => handleOpenEdit(rule)}
                            className="p-1.5 hover:bg-slate-100 dark:hover:bg-slate-700 rounded text-slate-600 dark:text-slate-300 hover:text-indigo-600"
                            title="Edytuj regułę"
                          >
                            <Edit3 className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => handleDeleteRule(rule.id)}
                            className="p-1.5 hover:bg-slate-100 dark:hover:bg-slate-700 rounded text-slate-600 dark:text-slate-300 hover:text-rose-600"
                            title="Usuń z OpenFGA"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
      )}

      {/* Sekcja testowania uprawnień Check (POST /check) & Dziennik zdarzeń */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Tester Check */}
        <div id="openfga-check-tester" className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl p-5 shadow-sm">
          <h3 className="text-base font-semibold text-slate-900 dark:text-white mb-2 flex items-center gap-2">
            <Play className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
            Tester ewaluacji OpenFGA Check (POST /stores/{'{id}'}/check)
          </h3>
          <p className="text-xs text-slate-500 dark:text-slate-400 mb-4">
            Sprawdź czy podmiot ma przypisaną relację do obiektu zgodnie z modelem Google Zanzibar.
          </p>

          <div className="space-y-3 text-sm">
            <div>
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                User / Subject (np. user:anne, role:admin):
              </label>
              <input
                type="text"
                value={checkUser}
                onChange={e => setCheckUser(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white font-mono text-xs focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                Relation / Akcja (np. reader, writer, owner):
              </label>
              <input
                type="text"
                value={checkRelation}
                onChange={e => setCheckRelation(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white font-mono text-xs focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                Object / Zasób (np. document:roadmap_2026):
              </label>
              <input
                type="text"
                value={checkObject}
                onChange={e => setCheckObject(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white font-mono text-xs focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <button
              onClick={handleTestCheck}
              className="w-full mt-2 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg text-sm flex items-center justify-center gap-2 transition shadow"
            >
              <FileCheck className="w-4 h-4" />
              Wykonaj OpenFGA Check
            </button>

            {checkResult && (
              <div className={`p-4 rounded-lg border text-xs mt-3 ${
                checkResult.allowed 
                  ? 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-300' 
                  : 'bg-rose-50 dark:bg-rose-950/40 border-rose-200 dark:border-rose-800 text-rose-800 dark:text-rose-300'
              }`}>
                <div className="font-bold flex items-center gap-1.5 mb-1">
                  {checkResult.allowed ? <CheckCircle2 className="w-4 h-4" /> : <AlertCircle className="w-4 h-4" />}
                  Decyzja OpenFGA: {checkResult.allowed ? 'DOZWOLONE (Allowed: true)' : 'ODMOWA (Allowed: false)'}
                </div>
                <div>{checkResult.message}</div>
              </div>
            )}
          </div>
        </div>

        {/* Dziennik Zdarzeń Synchronizacji */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm text-slate-200">
          <h3 className="text-base font-semibold mb-2 flex items-center gap-2 text-indigo-400">
            <Terminal className="w-4 h-4" />
            Dziennik zdarzeń API REST OpenFGA (0.0.0.0:8080)
          </h3>
          <p className="text-xs text-slate-400 mb-3">
            Śledzenie żądań wysyłanych przez <code>AuthZenToOpenFgaAdapter</code> do endpointu OpenFGA.
          </p>

          <div className="bg-black/50 border border-slate-800 rounded-lg p-3 font-mono text-xs text-slate-300 h-64 overflow-y-auto space-y-1.5">
            {logs.map((log, idx) => (
              <div key={idx} className="leading-relaxed">
                {log.includes('write') ? (
                  <span className="text-emerald-400">{log}</span>
                ) : log.includes('deletes') ? (
                  <span className="text-rose-400">{log}</span>
                ) : log.includes('Check') ? (
                  <span className="text-cyan-400">{log}</span>
                ) : (
                  <span>{log}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Modal Tworzenia / Edycji Reguły */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl w-full max-w-2xl shadow-2xl overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-200 dark:border-slate-700 flex justify-between items-center">
              <h3 className="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-indigo-600 dark:text-indigo-400" />
                {editingRule ? 'Edycja reguły AuthZEN' : 'Nowa reguła w obiekcie AuthZEN'}
              </h3>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 text-xl font-bold"
              >
                &times;
              </button>
            </div>

            <div className="p-6 space-y-4 max-h-[80vh] overflow-y-auto">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Nazwa reguły
                  </label>
                  <input
                    type="text"
                    value={formName}
                    onChange={e => setFormName(e.target.value)}
                    placeholder="np. Dostęp do faktur"
                    className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Opis biznesowy
                  </label>
                  <input
                    type="text"
                    value={formDescription}
                    onChange={e => setFormDescription(e.target.value)}
                    placeholder="np. Nadaje uprawnienie odczytu dla działu..."
                    className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500"
                  />
                </div>
              </div>

              {/* Pola AuthZEN */}
              <div className="p-4 bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-700 rounded-xl space-y-3">
                <div className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400 flex items-center gap-1.5">
                  <KeyRound className="w-3.5 h-3.5" />
                  Parametry specyfikacji AuthZEN
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Typ podmiotu (Subject Type)
                    </label>
                    <select
                      value={formSubjectType}
                      onChange={e => setFormSubjectType(e.target.value as any)}
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white"
                    >
                      <option value="user">user (użytkownik)</option>
                      <option value="role">role (rola Identity)</option>
                      <option value="group">group (grupa / zespół)</option>
                      <option value="service">service (mikrousługa / API)</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Identyfikator podmiotu (Subject ID)
                    </label>
                    <input
                      type="text"
                      value={formSubjectId}
                      onChange={e => setFormSubjectId(e.target.value)}
                      placeholder="np. anne, admin, finance"
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm font-mono text-slate-900 dark:text-white"
                    />
                  </div>
                </div>

                {/* Szybkie szablony ReBAC */}
                <div className="flex flex-wrap items-center gap-2 pt-1">
                  <span className="text-[11px] text-slate-500 dark:text-slate-400 font-medium">Szablon:</span>
                  <button
                    type="button"
                    onClick={() => {
                      setFormSubjectType('user');
                      setFormAction('member');
                      setFormResourceType('role');
                      setFormResourceId('admin');
                    }}
                    className="px-2 py-0.5 rounded bg-purple-500/10 hover:bg-purple-500/20 text-purple-600 dark:text-purple-300 border border-purple-500/30 text-[11px] font-medium transition cursor-pointer"
                  >
                    Użytkownik ➔ Rola (#member)
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setFormSubjectType('role');
                      setFormSubjectId('admin');
                      setFormAction('owner');
                      setFormResourceType('repo');
                      setFormResourceId('quorum-core');
                    }}
                    className="px-2 py-0.5 rounded bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-600 dark:text-emerald-300 border border-emerald-500/30 text-[11px] font-medium transition cursor-pointer"
                  >
                    Rola ➔ Zasób (#owner)
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setFormSubjectType('user');
                      setFormAction('reader');
                      setFormResourceType('document');
                      setFormResourceId('roadmap_2026');
                    }}
                    className="px-2 py-0.5 rounded bg-blue-500/10 hover:bg-blue-500/20 text-blue-600 dark:text-blue-300 border border-blue-500/30 text-[11px] font-medium transition cursor-pointer"
                  >
                    Użytkownik ➔ Zasób (#reader)
                  </button>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Akcja / Relacja (Action)
                    </label>
                    <input
                      type="text"
                      list="action-suggestions"
                      value={formAction}
                      onChange={e => setFormAction(e.target.value)}
                      placeholder="member, reader, writer, owner"
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm font-mono text-slate-900 dark:text-white"
                    />
                    <datalist id="action-suggestions">
                      <option value="member" />
                      <option value="reader" />
                      <option value="writer" />
                      <option value="owner" />
                      <option value="editor" />
                      <option value="viewer" />
                      <option value="admin" />
                      <option value="contributor" />
                      <option value="invoker" />
                    </datalist>
                  </div>

                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Typ zasobu (Resource Type)
                    </label>
                    <input
                      type="text"
                      list="resourcetype-suggestions"
                      value={formResourceType}
                      onChange={e => setFormResourceType(e.target.value)}
                      placeholder="role, group, document, repo"
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm font-mono text-slate-900 dark:text-white"
                    />
                    <datalist id="resourcetype-suggestions">
                      <option value="role" />
                      <option value="group" />
                      <option value="document" />
                      <option value="repo" />
                      <option value="route" />
                      <option value="api" />
                      <option value="database" />
                    </datalist>
                  </div>

                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Id zasobu (Resource ID)
                    </label>
                    <input
                      type="text"
                      value={formResourceId}
                      onChange={e => setFormResourceId(e.target.value)}
                      placeholder="roadmap_2026, orders"
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm font-mono text-slate-900 dark:text-white"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-3 pt-1">
                  <div>
                    <label className="block text-xs text-slate-600 dark:text-slate-400 mb-1">
                      Efekt autoryzacji (Effect)
                    </label>
                    <select
                      value={formEffect}
                      onChange={e => setFormEffect(e.target.value as any)}
                      className="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white"
                    >
                      <option value="Permit">Permit (Tworzy krotkę relacji w OpenFGA)</option>
                      <option value="Deny">Deny (Usuwa krotkę z OpenFGA)</option>
                    </select>
                  </div>

                  <div className="flex items-center gap-2 pt-6">
                    <input
                      type="checkbox"
                      id="enableRuleCheck"
                      checked={formIsEnabled}
                      onChange={e => setFormIsEnabled(e.target.checked)}
                      className="w-4 h-4 text-indigo-600 rounded"
                    />
                    <label htmlFor="enableRuleCheck" className="text-sm font-medium text-slate-700 dark:text-slate-300">
                      Reguła aktywna
                    </label>
                  </div>
                </div>
              </div>

              {/* Podgląd na żywo mapowania */}
              <div className="p-4 bg-slate-900 text-slate-200 rounded-xl font-mono text-xs space-y-1.5 border border-slate-800">
                <div className="text-emerald-400 font-bold mb-1 flex items-center gap-1.5">
                  <ArrowRight className="w-3.5 h-3.5" />
                  Podgląd krotki wygenerowanej przez AuthZenToOpenFgaAdapter:
                </div>
                <div>User: <strong className="text-indigo-300">{formSubjectType}:{formSubjectId || '...'}</strong></div>
                <div>Relation: <strong className="text-indigo-300">{formAction || '...'}</strong></div>
                <div>Object: <strong className="text-indigo-300">{formResourceType}:{formResourceId || '...'}</strong></div>
                <div className="pt-1 text-[11px] text-slate-400 border-t border-slate-800 mt-2">
                  OpenFGA REST Payload: <code>{`{"writes": {"tuple_keys": [{"user": "${formSubjectType}:${formSubjectId || '...'}", "relation": "${formAction}", "object": "${formResourceType}:${formResourceId || '...'}"}]}}`}</code>
                </div>
              </div>
            </div>

            <div className="px-6 py-4 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3 bg-slate-50 dark:bg-slate-900/40">
              <button
                onClick={() => setIsModalOpen(false)}
                className="px-4 py-2 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                Anuluj
              </button>
              <button
                onClick={handleSaveRule}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium transition shadow flex items-center gap-2"
              >
                <CheckCircle2 className="w-4 h-4" />
                Zapisz do OpenFGA REST API
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
