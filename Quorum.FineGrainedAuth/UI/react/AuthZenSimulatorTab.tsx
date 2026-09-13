import React, { useState, useMemo } from 'react';
import { 
  ShieldCheck, 
  ShieldAlert, 
  UserCheck, 
  UserX, 
  Play, 
  RefreshCw, 
  Layers, 
  CheckCircle2, 
  XCircle, 
  AlertTriangle, 
  Code2, 
  Copy, 
  Check, 
  Lock, 
  Sparkles, 
  ArrowRight, 
  Clock, 
  Sliders, 
  Users, 
  FileText, 
  Plus, 
  ToggleLeft, 
  ToggleRight,
  Database,
  Search,
  ExternalLink,
  ChevronRight,
  Server
} from 'lucide-react';
import { 
  IdentityUser, 
  AuthZenPolicy, 
  AuthZenEvaluationRequest, 
  PdpEvaluationResult 
} from '../../types/authzen';
import { 
  INITIAL_IDENTITY_USERS, 
  INITIAL_AUTHZEN_POLICIES, 
  PRESET_SCENARIOS, 
  evaluateAuthZenPolicy 
} from '../../data/authZenData';

export const AuthZenSimulatorTab: React.FC = () => {
  // Stan danych
  const [identityUsers] = useState<IdentityUser[]>(INITIAL_IDENTITY_USERS);
  const [policies, setPolicies] = useState<AuthZenPolicy[]>(INITIAL_AUTHZEN_POLICIES);
  
  // Stan formularza ewaluacji
  const [selectedUserId, setSelectedUserId] = useState<string>('usr_01_admin');
  const [actionName, setActionName] = useState<string>('GET');
  const [resourceId, setResourceId] = useState<string>('/api/v1/billing/invoices/2026');
  const [resourceType, setResourceType] = useState<string>('route');
  
  // Stan pod-zakładek w AdminUI
  const [adminSubTab, setAdminSubTab] = useState<'simulator' | 'policies' | 'users' | 'architecture'>('simulator');
  
  // Stan wyniku ewaluacji
  const [evalResult, setEvalResult] = useState<PdpEvaluationResult | null>(() => {
    const initialReq: AuthZenEvaluationRequest = {
      subject: { type: 'user', id: 'usr_01_admin' },
      action: { name: 'GET' },
      resource: { type: 'route', id: '/api/v1/billing/invoices/2026' }
    };
    return evaluateAuthZenPolicy(initialReq, INITIAL_AUTHZEN_POLICIES, INITIAL_IDENTITY_USERS);
  });
  
  const [isEvaluating, setIsEvaluating] = useState<boolean>(false);
  const [copiedRequest, setCopiedRequest] = useState<boolean>(false);
  const [copiedResponse, setCopiedResponse] = useState<boolean>(false);
  const [activeJsonTab, setActiveJsonTab] = useState<'response' | 'request'>('response');

  // Aktywny użytkownik PIP na podstawie wyboru
  const selectedUser = useMemo(() => {
    return identityUsers.find(u => u.id === selectedUserId) || null;
  }, [identityUsers, selectedUserId]);

  // Wykonanie ewaluacji PDP
  const handleRunEvaluation = () => {
    setIsEvaluating(true);
    setTimeout(() => {
      const req: AuthZenEvaluationRequest = {
        subject: {
          type: 'user',
          id: selectedUserId
        },
        action: {
          name: actionName
        },
        resource: {
          type: resourceType,
          id: resourceId
        },
        context: {
          environment: 'production',
          clientIp: '192.168.1.105',
          requestTime: new Date().toISOString()
        }
      };

      const result = evaluateAuthZenPolicy(req, policies, identityUsers);
      setEvalResult(result);
      setIsEvaluating(false);
    }, 150);
  };

  // Zastosowanie scenariusza testowego
  const handleApplyPreset = (presetId: string) => {
    const preset = PRESET_SCENARIOS.find(p => p.id === presetId);
    if (!preset) return;

    setSelectedUserId(preset.userId);
    setActionName(preset.action);
    setResourceId(preset.resourceId);

    const req: AuthZenEvaluationRequest = {
      subject: {
        type: 'user',
        id: preset.userId
      },
      action: {
        name: preset.action
      },
      resource: {
        type: 'route',
        id: preset.resourceId
      },
      context: {
        environment: 'production',
        scenarioPreset: preset.title,
        requestTime: new Date().toISOString()
      }
    };

    const result = evaluateAuthZenPolicy(req, policies, identityUsers);
    setEvalResult(result);
  };

  // Przełączenie aktywności polityki PAP
  const handleTogglePolicy = (policyId: number) => {
    const updated = policies.map(p => {
      if (p.id === policyId) {
        return { ...p, isEnabled: !p.isEnabled };
      }
      return p;
    });
    setPolicies(updated);

    // Natychmiastowe odświeżenie ewaluacji z nowym stanem polityk
    if (evalResult) {
      const req: AuthZenEvaluationRequest = {
        subject: { type: 'user', id: selectedUserId },
        action: { name: actionName },
        resource: { type: resourceType, id: resourceId }
      };
      setEvalResult(evaluateAuthZenPolicy(req, updated, identityUsers));
    }
  };

  const handleCopyJson = (text: string, isReq: boolean) => {
    navigator.clipboard.writeText(text);
    if (isReq) {
      setCopiedRequest(true);
      setTimeout(() => setCopiedRequest(false), 2000);
    } else {
      setCopiedResponse(true);
      setTimeout(() => setCopiedResponse(false), 2000);
    }
  };

  return (
    <div className="flex flex-col gap-6 w-full">
      {/* Top Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-slate-900 to-indigo-950/70 border border-slate-800 rounded-2xl p-6 shadow-lg">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div className="space-y-1.5">
            <div className="flex items-center gap-2">
              <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                IETF / OpenID AuthZEN 1.0
              </span>
              <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-500/10 text-blue-400 border border-blue-500/20">
                PDP & PIP Engine
              </span>
              <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-purple-500/10 text-purple-400 border border-purple-500/20">
                ASP.NET Identity Integration
              </span>
            </div>
            <h2 className="text-2xl font-bold text-white tracking-tight flex items-center gap-2">
              <ShieldCheck className="w-7 h-7 text-emerald-400" />
              AdminUI – Symulator Decyzji Polityk AuthZEN
            </h2>
            <p className="text-sm text-slate-300 max-w-3xl leading-relaxed">
              Interaktywny panel Policy Administration Point (PAP) oraz symulator Policy Decision Point (PDP). 
              Wybierz tożsamość użytkownika z bazy <strong>ASP.NET Core Identity (PIP)</strong>, żądaną akcję oraz docelowy zasób API Gateway, 
              aby sprawdzić ewaluację reguł w czasie rzeczywistym.
            </p>
          </div>

          <div className="flex flex-wrap lg:flex-col items-end gap-2 text-xs">
            <div className="flex items-center gap-2 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
              <div className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
              <span className="text-slate-300">Silnik PDP: <strong>Aktywny (.NET 10)</strong></span>
            </div>
            <div className="flex items-center gap-2 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
              <span className="text-slate-400">Aktywne polityki:</span>
              <span className="text-emerald-400 font-bold">{policies.filter(p => p.isEnabled).length} z {policies.length}</span>
            </div>
          </div>
        </div>

        {/* Sub-Navigation Tabs */}
        <div className="flex flex-wrap gap-2 border-t border-slate-800/90 mt-6 pt-4">
          <button
            onClick={() => setAdminSubTab('simulator')}
            className={`flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-medium rounded-lg transition-colors cursor-pointer ${
              adminSubTab === 'simulator'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-600/30'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Play className="w-4 h-4" />
            <span>Symulator Decyzji (PDP)</span>
          </button>
          <button
            onClick={() => setAdminSubTab('policies')}
            className={`flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-medium rounded-lg transition-colors cursor-pointer ${
              adminSubTab === 'policies'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-600/30'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Sliders className="w-4 h-4" />
            <span>Polityki Autoryzacji PAP ({policies.length})</span>
          </button>
          <button
            onClick={() => setAdminSubTab('users')}
            className={`flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-medium rounded-lg transition-colors cursor-pointer ${
              adminSubTab === 'users'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-600/30'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Users className="w-4 h-4" />
            <span>Użytkownicy Identity (PIP)</span>
          </button>
          <button
            onClick={() => setAdminSubTab('architecture')}
            className={`flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-medium rounded-lg transition-colors cursor-pointer ${
              adminSubTab === 'architecture'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-600/30'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Layers className="w-4 h-4" />
            <span>Architektura PEP / PDP / PIP</span>
          </button>
        </div>
      </div>

      {/* SUB-TAB 1: PDP DECISION SIMULATOR */}
      {adminSubTab === 'simulator' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          {/* Left Column: Evaluation Request Form (7 cols) */}
          <div className="lg:col-span-7 flex flex-col gap-5">
            {/* Scenarios Preset Strip */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-sm">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
                  <Sparkles className="w-3.5 h-3.5 text-amber-400" /> Szybkie scenariusze testowe
                </span>
                <span className="text-[11px] text-slate-500">Kliknij scenariusz aby załadować parametry</span>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {PRESET_SCENARIOS.map(preset => (
                  <button
                    key={preset.id}
                    onClick={() => handleApplyPreset(preset.id)}
                    className="flex flex-col text-left p-2.5 rounded-lg bg-slate-950/70 hover:bg-slate-800 border border-slate-800/80 hover:border-slate-700 transition-all cursor-pointer group"
                  >
                    <div className="flex items-center justify-between w-full">
                      <span className="text-xs font-medium text-slate-200 group-hover:text-blue-400 truncate">
                        {preset.title}
                      </span>
                      <span className={`text-[10px] px-1.5 py-0.2 rounded font-mono font-bold ${
                        preset.expectedDecision ? 'bg-emerald-950 text-emerald-400 border border-emerald-800/50' : 'bg-rose-950 text-rose-400 border border-rose-800/50'
                      }`}>
                        {preset.expectedDecision ? 'Permit' : 'Deny'}
                      </span>
                    </div>
                    <span className="text-[11px] text-slate-400 line-clamp-1 mt-1">
                      {preset.description}
                    </span>
                  </button>
                ))}
              </div>
            </div>

            {/* Main Interactive Form Card */}
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 shadow-sm flex flex-col gap-5">
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <h3 className="text-base font-bold text-white flex items-center gap-2">
                  <Sliders className="w-4 h-4 text-blue-400" />
                  Parametry Żądania Autoryzacyjnego (AuthZEN Evaluation Request)
                </h3>
                <span className="text-xs text-slate-400">spec: <code>evaluation/v1</code></span>
              </div>

              {/* 1. Subject / Identity User Selection */}
              <div className="flex flex-col gap-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-300 flex items-center justify-between">
                  <span className="flex items-center gap-1.5">
                    <Users className="w-3.5 h-3.5 text-blue-400" />
                    1. Podmiot (Subject) – Wybierz użytkownika z bazy ASP.NET Identity:
                  </span>
                  {selectedUser && (
                    <span className="text-[11px] font-normal text-slate-400">
                      ID: <code className="text-blue-400">{selectedUser.id}</code>
                    </span>
                  )}
                </label>

                <select
                  value={selectedUserId}
                  onChange={(e) => setSelectedUserId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-blue-500 transition-colors cursor-pointer"
                >
                  <option value="">(Anonimowy / Brak uwierzytelnienia)</option>
                  {identityUsers.map(user => (
                    <option key={user.id} value={user.id}>
                      {user.fullName} — {user.userName} ({user.email}) [{user.roles.join(', ')}] {user.isLockedOut ? '⛔ [ZABLOKOWANY]' : '✓ [Aktywny]'}
                    </option>
                  ))}
                </select>

                {/* Selected Identity PIP Card */}
                {selectedUser ? (
                  <div className="bg-slate-950/80 rounded-xl p-3 border border-slate-800 text-xs flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className={`w-9 h-9 rounded-lg bg-gradient-to-br ${selectedUser.avatarColor} flex items-center justify-center text-white font-bold text-sm shadow-sm`}>
                        {selectedUser.userName.slice(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-slate-200">{selectedUser.fullName}</span>
                          {selectedUser.isLockedOut ? (
                            <span className="flex items-center gap-1 px-1.5 py-0.5 text-[10px] font-semibold bg-rose-500/20 text-rose-400 border border-rose-500/30 rounded">
                              <Lock className="w-2.5 h-2.5" /> Lockout: True
                            </span>
                          ) : (
                            <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 rounded">
                              Active
                            </span>
                          )}
                        </div>
                        <div className="text-slate-400 text-[11px] mt-0.5">
                          Login: <code className="text-slate-300">{selectedUser.userName}</code> • Email: <code className="text-slate-300">{selectedUser.email}</code>
                        </div>
                      </div>
                    </div>

                    <div className="flex flex-wrap gap-1 items-center">
                      <span className="text-[11px] text-slate-400 mr-1">Role PIP:</span>
                      {selectedUser.roles.map(r => (
                        <span key={r} className="px-2 py-0.5 text-[10px] font-semibold rounded bg-blue-500/15 text-blue-300 border border-blue-500/30">
                          {r}
                        </span>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="bg-amber-950/20 border border-amber-800/40 rounded-lg p-2.5 text-xs text-amber-300 flex items-center gap-2">
                    <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0" />
                    <span>Żądanie anonimowe bez tożsamości w nagłówku. Polityki wymagające uwierzytelnienia odrzucą dostęp.</span>
                  </div>
                )}
              </div>

              {/* 2. Action Input & Quick Select */}
              <div className="flex flex-col gap-2">
                <div className="flex items-center justify-between">
                  <label className="text-xs font-bold uppercase tracking-wider text-slate-300 flex items-center gap-1.5">
                    <Code2 className="w-3.5 h-3.5 text-emerald-400" />
                    2. Akcja (Action):
                  </label>
                  <div className="flex flex-wrap gap-1">
                    {['GET', 'POST', 'PUT', 'DELETE', 'read', 'write', 'execute', '*'].map(act => (
                      <button
                        key={act}
                        type="button"
                        onClick={() => setActionName(act)}
                        className={`px-2 py-0.5 text-[11px] rounded font-mono transition-colors cursor-pointer ${
                          actionName === act 
                            ? 'bg-emerald-600 text-white font-bold' 
                            : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
                        }`}
                      >
                        {act}
                      </button>
                    ))}
                  </div>
                </div>
                <input
                  type="text"
                  value={actionName}
                  onChange={(e) => setActionName(e.target.value)}
                  placeholder="np. GET, read, write, execute"
                  className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-sm font-mono text-emerald-400 focus:outline-none focus:border-emerald-500"
                />
              </div>

              {/* 3. Target Resource Input & Quick Select */}
              <div className="flex flex-col gap-2">
                <div className="flex items-center justify-between">
                  <label className="text-xs font-bold uppercase tracking-wider text-slate-300 flex items-center gap-1.5">
                    <Server className="w-3.5 h-3.5 text-indigo-400" />
                    3. Identyfikator Zasobu Docelowego (Target Resource):
                  </label>
                  <div className="flex items-center gap-1 text-[11px] text-slate-400">
                    Typ:
                    <select
                      value={resourceType}
                      onChange={(e) => setResourceType(e.target.value)}
                      className="bg-slate-800 text-slate-200 rounded px-1.5 py-0.5 border border-slate-700 text-[11px]"
                    >
                      <option value="route">route</option>
                      <option value="api">api</option>
                      <option value="document">document</option>
                    </select>
                  </div>
                </div>

                <div className="flex flex-wrap gap-1.5 mb-1">
                  {[
                    '/api/v1/billing/invoices/2026',
                    '/api/v1/orders',
                    '/api/v1/orders/501',
                    '/api/v1/devices/telemetry',
                    '/public/health',
                    '/admin/dashboard'
                  ].map(res => (
                    <button
                      key={res}
                      type="button"
                      onClick={() => setResourceId(res)}
                      className={`px-2 py-0.5 text-[11px] rounded font-mono transition-colors truncate max-w-xs cursor-pointer ${
                        resourceId === res 
                          ? 'bg-indigo-600 text-white font-bold' 
                          : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
                      }`}
                    >
                      {res}
                    </button>
                  ))}
                </div>

                <input
                  type="text"
                  value={resourceId}
                  onChange={(e) => setResourceId(e.target.value)}
                  placeholder="np. /api/v1/orders lub /api/v1/billing/invoices"
                  className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-sm font-mono text-indigo-300 focus:outline-none focus:border-indigo-500"
                />
              </div>

              {/* Action Buttons */}
              <div className="flex items-center justify-between pt-3 border-t border-slate-800">
                <button
                  type="button"
                  onClick={() => {
                    setSelectedUserId('usr_01_admin');
                    setActionName('GET');
                    setResourceId('/api/v1/billing/invoices/2026');
                    handleRunEvaluation();
                  }}
                  className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium text-slate-400 hover:text-white bg-slate-800/60 hover:bg-slate-800 rounded-lg transition-colors cursor-pointer"
                >
                  <RefreshCw className="w-3.5 h-3.5" /> Przywróć domyślne
                </button>

                <button
                  type="button"
                  onClick={handleRunEvaluation}
                  disabled={isEvaluating}
                  className="flex items-center gap-2 px-5 py-2.5 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-semibold text-sm rounded-xl shadow-lg shadow-blue-600/30 transition-all cursor-pointer disabled:opacity-50"
                >
                  {isEvaluating ? (
                    <>
                      <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                      <span>Ewaluacja w toku...</span>
                    </>
                  ) : (
                    <>
                      <Play className="w-4 h-4 fill-white" />
                      <span>Wykonaj Ewaluację AuthZEN (PDP)</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          </div>

          {/* Right Column: Evaluation Results & Trace (5 cols) */}
          <div className="lg:col-span-5 flex flex-col gap-5">
            {evalResult ? (
              <>
                {/* Decision Result Card */}
                <div className={`rounded-2xl border p-5 shadow-lg transition-all ${
                  evalResult.decision
                    ? 'bg-gradient-to-br from-emerald-950/50 via-slate-900 to-slate-900 border-emerald-500/50 shadow-emerald-500/10'
                    : 'bg-gradient-to-br from-rose-950/50 via-slate-900 to-slate-900 border-rose-500/50 shadow-rose-500/10'
                }`}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className={`w-12 h-12 rounded-xl flex items-center justify-center shadow-lg ${
                        evalResult.decision 
                          ? 'bg-emerald-500 text-white shadow-emerald-500/30' 
                          : 'bg-rose-500 text-white shadow-rose-500/30'
                      }`}>
                        {evalResult.decision ? (
                          <CheckCircle2 className="w-7 h-7" />
                        ) : (
                          <XCircle className="w-7 h-7" />
                        )}
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h3 className={`text-xl font-black tracking-tight ${
                            evalResult.decision ? 'text-emerald-400' : 'text-rose-400'
                          }`}>
                            {evalResult.decision ? 'DECYZJA: PERMIT (ZEZWOLONO)' : 'DECYZJA: DENY (ODMÓWIONO)'}
                          </h3>
                        </div>
                        <p className="text-xs text-slate-300 mt-0.5">
                          {evalResult.decision 
                            ? 'PEP w bramce przepuści żądanie do serwisu docelowego (HTTP 200 OK).' 
                            : 'PEP natychmiast zablokuje żądanie kodem HTTP 403 Forbidden.'}
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Quick Metrics Strip */}
                  <div className="grid grid-cols-2 gap-2 mt-4 pt-4 border-t border-slate-800">
                    <div className="bg-slate-950/70 p-2 rounded-lg border border-slate-800">
                      <span className="text-[10px] text-slate-400 block uppercase">Czas ewaluacji PDP:</span>
                      <span className="text-xs font-mono font-bold text-white flex items-center gap-1 mt-0.5">
                        <Clock className="w-3 h-3 text-emerald-400" />
                        {evalResult.executionTimeMs} ms
                      </span>
                    </div>
                    <div className="bg-slate-950/70 p-2 rounded-lg border border-slate-800">
                      <span className="text-[10px] text-slate-400 block uppercase">Dopasowana Polityka:</span>
                      <span className="text-xs font-mono font-bold text-blue-400 truncate block mt-0.5">
                        {evalResult.matchedPolicy ? evalResult.matchedPolicy.name : 'Brak (Default Deny)'}
                      </span>
                    </div>
                  </div>

                  {/* Explanation box */}
                  <div className="mt-3 p-3 rounded-xl bg-slate-950/90 border border-slate-800/80 text-xs">
                    <span className="text-slate-400 text-[10px] uppercase font-bold tracking-wider block mb-1">
                      Uzasadnienie Silnika PDP (Reason):
                    </span>
                    <p className="text-slate-200 leading-relaxed font-sans">
                      {evalResult.reason}
                    </p>
                  </div>
                </div>

                {/* PDP Evaluation Trace (Step-by-Step Policy Walkthrough) */}
                <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-sm">
                  <div className="flex items-center justify-between mb-3 border-b border-slate-800 pb-2.5">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-slate-300 flex items-center gap-1.5">
                      <Layers className="w-3.5 h-3.5 text-blue-400" />
                      Ścieżka Ewaluacji Polityk (PDP Evaluation Trace)
                    </h4>
                    <span className="text-[10px] text-slate-500 font-mono">
                      Wg priorytetu (malejąco)
                    </span>
                  </div>

                  <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                    {evalResult.trace.map((step, idx) => (
                      <div
                        key={step.policyId}
                        className={`p-2.5 rounded-lg border text-xs transition-all ${
                          step.result === 'Permit'
                            ? 'bg-emerald-950/30 border-emerald-600/50'
                            : step.result === 'Deny'
                            ? 'bg-rose-950/30 border-rose-600/50'
                            : step.result === 'Mismatch'
                            ? 'bg-amber-950/20 border-amber-800/30 opacity-80'
                            : 'bg-slate-950/40 border-slate-800/60 opacity-60'
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-1.5">
                            <span className="text-[10px] font-mono px-1.5 py-0.2 bg-slate-800 text-slate-400 rounded">
                              #{idx + 1}
                            </span>
                            <span className="font-semibold text-slate-200">
                              {step.policyName}
                            </span>
                            <span className="text-[10px] font-mono text-slate-400">
                              (Prio: {step.priority})
                            </span>
                          </div>

                          <span className={`px-2 py-0.5 text-[10px] font-bold rounded ${
                            step.result === 'Permit'
                              ? 'bg-emerald-500 text-white'
                              : step.result === 'Deny'
                              ? 'bg-rose-500 text-white'
                              : step.result === 'Mismatch'
                              ? 'bg-amber-500/20 text-amber-300'
                              : 'bg-slate-800 text-slate-400'
                          }`}>
                            {step.result}
                          </span>
                        </div>

                        {/* Conditions matching checklist */}
                        <div className="flex items-center gap-3 mt-1.5 text-[11px] text-slate-400 font-mono">
                          <span className={`flex items-center gap-1 ${step.resourceMatched ? 'text-emerald-400' : 'text-slate-500'}`}>
                            {step.resourceMatched ? '✓' : '✕'} Zasób
                          </span>
                          <span className={`flex items-center gap-1 ${step.actionMatched ? 'text-emerald-400' : 'text-slate-500'}`}>
                            {step.actionMatched ? '✓' : '✕'} Akcja
                          </span>
                          <span className={`flex items-center gap-1 ${step.subjectMatched ? 'text-emerald-400' : 'text-slate-500'}`}>
                            {step.subjectMatched ? '✓' : '✕'} Podmiot
                          </span>
                        </div>

                        <div className="text-[11px] text-slate-300 mt-1">
                          {step.reason}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Raw JSON Inspector Tabs */}
                <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-sm">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => setActiveJsonTab('response')}
                        className={`text-xs font-semibold px-2.5 py-1 rounded transition-colors cursor-pointer ${
                          activeJsonTab === 'response' 
                            ? 'bg-blue-600/30 text-blue-400 border border-blue-500/40' 
                            : 'text-slate-400 hover:text-slate-200'
                        }`}
                      >
                        AuthZEN Response (JSON)
                      </button>
                      <button
                        onClick={() => setActiveJsonTab('request')}
                        className={`text-xs font-semibold px-2.5 py-1 rounded transition-colors cursor-pointer ${
                          activeJsonTab === 'request' 
                            ? 'bg-blue-600/30 text-blue-400 border border-blue-500/40' 
                            : 'text-slate-400 hover:text-slate-200'
                        }`}
                      >
                        AuthZEN Request (JSON)
                      </button>
                    </div>

                    <button
                      onClick={() => handleCopyJson(
                        activeJsonTab === 'response' 
                          ? JSON.stringify(evalResult.responsePayload, null, 2)
                          : JSON.stringify(evalResult.requestPayload, null, 2),
                        activeJsonTab === 'request'
                      )}
                      className="flex items-center gap-1 text-[11px] text-slate-400 hover:text-white px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 transition-colors cursor-pointer"
                    >
                      {(activeJsonTab === 'response' ? copiedResponse : copiedRequest) ? (
                        <>
                          <Check className="w-3 h-3 text-emerald-400" />
                          <span>Skopiowano</span>
                        </>
                      ) : (
                        <>
                          <Copy className="w-3 h-3" />
                          <span>Kopiuj</span>
                        </>
                      )}
                    </button>
                  </div>

                  <pre className="bg-slate-950 text-slate-200 font-mono text-[11px] p-3 rounded-lg border border-slate-800 overflow-x-auto max-h-56">
                    {activeJsonTab === 'response' 
                      ? JSON.stringify(evalResult.responsePayload, null, 2)
                      : JSON.stringify(evalResult.requestPayload, null, 2)}
                  </pre>
                </div>
              </>
            ) : (
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-8 text-center text-slate-400 flex flex-col items-center justify-center gap-3">
                <ShieldCheck className="w-12 h-12 text-slate-600" />
                <p>Kliknij przycisk „Wykonaj Ewaluację AuthZEN (PDP)”, aby zobaczyć wynik decyzyjny.</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* SUB-TAB 2: POLICIES MANAGEMENT (PAP) */}
      {adminSubTab === 'policies' && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-sm flex flex-col gap-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-4">
            <div>
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <Sliders className="w-5 h-5 text-emerald-400" />
                Punkt Administracji Politykami (Policy Administration Point – PAP)
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Definiuj reguły RBAC/ABAC oceniane przez silnik PDP. Możesz włączać i wyłączać poszczególne reguły, aby badać wpływ na decyzje autoryzacyjne.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs bg-slate-800 text-slate-300 px-3 py-1.5 rounded-lg border border-slate-700">
                Łącznie polityk: <strong>{policies.length}</strong>
              </span>
            </div>
          </div>

          <div className="space-y-3">
            {policies.map(policy => (
              <div
                key={policy.id}
                className={`p-4 rounded-xl border transition-all ${
                  policy.isEnabled 
                    ? 'bg-slate-950/70 border-slate-800 hover:border-slate-700' 
                    : 'bg-slate-950/30 border-slate-800/40 opacity-50'
                }`}
              >
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-mono text-xs px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-bold">
                        Prio: {policy.priority}
                      </span>
                      <span className="font-bold text-slate-100 text-sm">{policy.name}</span>
                      <span className={`px-2 py-0.5 text-xs font-bold rounded-full ${
                        policy.effect === 'Permit' 
                          ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' 
                          : 'bg-rose-500/20 text-rose-300 border border-rose-500/30'
                      }`}>
                        {policy.effect}
                      </span>
                    </div>
                    <p className="text-xs text-slate-400">{policy.description}</p>
                  </div>

                  <div className="flex items-center gap-3 shrink-0">
                    <button
                      onClick={() => handleTogglePolicy(policy.id)}
                      className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                        policy.isEnabled
                          ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 hover:bg-emerald-500/20'
                          : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
                      }`}
                    >
                      {policy.isEnabled ? (
                        <>
                          <ToggleRight className="w-4 h-4 text-emerald-400" />
                          <span>Aktywna (Włączona)</span>
                        </>
                      ) : (
                        <>
                          <ToggleLeft className="w-4 h-4 text-slate-500" />
                          <span>Wyłączona</span>
                        </>
                      )}
                    </button>
                  </div>
                </div>

                {/* Policy Rules Grid */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-3 pt-3 border-t border-slate-900 text-xs font-mono">
                  <div>
                    <span className="text-slate-500 block text-[10px] uppercase">Wzorzec Zasobu:</span>
                    <code className="text-indigo-400">{policy.resourcePattern}</code>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[10px] uppercase">Dozwolona Akcja:</span>
                    <code className="text-emerald-400">{policy.action}</code>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[10px] uppercase">Wymagane Role (RBAC):</span>
                    <span className="text-slate-300">
                      {policy.subjectRoles.length > 0 ? policy.subjectRoles.join(', ') : '(Wszyscy / Dowolna rola)'}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* SUB-TAB 3: ASP.NET IDENTITY USERS (PIP) */}
      {adminSubTab === 'users' && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-sm flex flex-col gap-4">
          <div className="border-b border-slate-800 pb-4">
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Users className="w-5 h-5 text-blue-400" />
              Policy Information Point (PIP) – Konta w ASP.NET Core Identity
            </h3>
            <p className="text-xs text-slate-400 mt-0.5">
              Silnik AuthZen PIP automatycznie odpytuje serwis <code>UserManager&lt;ApplicationUser&gt;</code>, aby dostarczyć podmiotowi przypisane role, roszczenia (claims) oraz sprawdzić status blokady (Lockout).
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {identityUsers.map(user => (
              <div key={user.id} className="bg-slate-950 border border-slate-800 rounded-xl p-4 flex flex-col justify-between gap-3">
                <div>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className={`w-8 h-8 rounded-lg bg-gradient-to-br ${user.avatarColor} flex items-center justify-center text-white font-bold text-xs`}>
                        {user.userName.slice(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <div className="font-bold text-slate-100 text-sm">{user.fullName}</div>
                        <div className="text-xs text-slate-400">{user.email}</div>
                      </div>
                    </div>

                    <span className={`px-2 py-0.5 text-[10px] font-bold rounded ${
                      user.isLockedOut 
                        ? 'bg-rose-500/20 text-rose-400 border border-rose-500/30' 
                        : 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                    }`}>
                      {user.isLockedOut ? 'Lockout: Zablokowany' : 'Konto Aktywne'}
                    </span>
                  </div>

                  <div className="mt-3 pt-3 border-t border-slate-900 space-y-2 text-xs">
                    <div>
                      <span className="text-slate-500 text-[11px] block">Role użytkownika:</span>
                      <div className="flex flex-wrap gap-1 mt-1">
                        {user.roles.map(r => (
                          <span key={r} className="px-2 py-0.5 rounded bg-blue-600/20 text-blue-300 border border-blue-500/30 font-medium text-[11px]">
                            {r}
                          </span>
                        ))}
                      </div>
                    </div>

                    <div>
                      <span className="text-slate-500 text-[11px] block">Roszczenia Identity (Claims):</span>
                      <pre className="bg-slate-900/80 p-2 rounded text-[10px] text-slate-300 font-mono mt-1 overflow-x-auto">
                        {JSON.stringify(user.claims, null, 2)}
                      </pre>
                    </div>
                  </div>
                </div>

                <div className="pt-2 border-t border-slate-900 flex justify-end">
                  <button
                    onClick={() => {
                      setSelectedUserId(user.id);
                      setAdminSubTab('simulator');
                    }}
                    className="flex items-center gap-1 text-xs text-blue-400 hover:text-blue-300 font-semibold cursor-pointer"
                  >
                    <span>Testuj autoryzację tego użytkownika</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* SUB-TAB 4: ARCHITECTURE OVERVIEW */}
      {adminSubTab === 'architecture' && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-sm flex flex-col gap-6">
          <div>
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Layers className="w-5 h-5 text-purple-400" />
              Architektura AuthZEN w Solucji Quorum (.NET 10)
            </h3>
            <p className="text-xs text-slate-400 mt-1">
              Standard <strong>AuthZEN (OpenID Foundation)</strong> rozdziela odpowiedzialności kontroli dostępu na cztery współpracujące komponenty.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="bg-slate-950 p-4 rounded-xl border border-blue-500/30 flex flex-col gap-2">
              <div className="w-8 h-8 rounded-lg bg-blue-500/20 text-blue-400 flex items-center justify-center font-bold text-sm">
                PEP
              </div>
              <h4 className="text-sm font-bold text-white">Policy Enforcement Point</h4>
              <p className="text-xs text-slate-400 leading-relaxed">
                Zlokalizowany w <code>Proxy2ManyHostsMiddleware</code> (Quorum Gateway). 
                Przechwytuje żądania HTTP, wyciąga tożsamość z tokena Bearer i wstrzymuje ruch do czasu decyzji PDP.
              </p>
              <div className="text-[11px] font-mono text-blue-400 mt-auto pt-2 border-t border-slate-900">
                Tryb: Fail-Closed (403)
              </div>
            </div>

            <div className="bg-slate-950 p-4 rounded-xl border border-emerald-500/30 flex flex-col gap-2">
              <div className="w-8 h-8 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold text-sm">
                PDP
              </div>
              <h4 className="text-sm font-bold text-white">Policy Decision Point</h4>
              <p className="text-xs text-slate-400 leading-relaxed">
                Silnik decyzyjny udostępniony przez <code>AuthZenEvaluationController</code> pod adresem <code>POST /authzen/evaluation/v1/evaluations</code>. 
                Dokonuje ewaluacji według priorytetów.
              </p>
              <div className="text-[11px] font-mono text-emerald-400 mt-auto pt-2 border-t border-slate-900">
                Wynik: Permit / Deny
              </div>
            </div>

            <div className="bg-slate-950 p-4 rounded-xl border border-amber-500/30 flex flex-col gap-2">
              <div className="w-8 h-8 rounded-lg bg-amber-500/20 text-amber-400 flex items-center justify-center font-bold text-sm">
                PIP
              </div>
              <h4 className="text-sm font-bold text-white">Policy Information Point</h4>
              <p className="text-xs text-slate-400 leading-relaxed">
                Serwis <code>AuthZenIdentityPipService</code> zasilający silnik PDP atrybutami z bazy ASP.NET Core Identity: role, claims, lockout status oraz tenant.
              </p>
              <div className="text-[11px] font-mono text-amber-400 mt-auto pt-2 border-t border-slate-900">
                Źródło: AspNetUsers
              </div>
            </div>

            <div className="bg-slate-950 p-4 rounded-xl border border-purple-500/30 flex flex-col gap-2">
              <div className="w-8 h-8 rounded-lg bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold text-sm">
                PAP
              </div>
              <h4 className="text-sm font-bold text-white">Policy Administration Point</h4>
              <p className="text-xs text-slate-400 leading-relaxed">
                Komponenty Radzen Blazor w <code>Quorum.Backend.AdminUI</code> (Strony <code>AuthZenPoliciesList</code> oraz <code>AuthZenPolicyEdit</code>). 
                Przechowuje reguły w tabeli <code>AuthZenPolicies</code>.
              </p>
              <div className="text-[11px] font-mono text-purple-400 mt-auto pt-2 border-t border-slate-900">
                Persystencja: EF Core
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
