import React, { useState, useEffect, useMemo, useRef } from 'react';
import {
  Activity,
  Zap,
  Play,
  Pause,
  RefreshCw,
  PlusCircle,
  AlertTriangle,
  Flame,
  CheckCircle2,
  Trash2,
  Server,
  Clock,
  Radio,
  Sliders,
  Sparkles,
  TrendingDown,
  TrendingUp,
  BarChart3,
  ShieldCheck
} from 'lucide-react';
import {
  generateInitialTelemetryLogs,
  createSingleTelemetryEntry,
  aggregateTelemetryTimeSeries,
  computeEndpointStats,
  computeStatusCodeBreakdown,
  computeTelemetrySummary
} from '../../utils/telemetryMockData';
import { TelemetryLogEntry } from '../../types/telemetry';
import { LatencyChart } from './LatencyChart';
import { ErrorRateChart } from './ErrorRateChart';
import { EndpointStatsChart } from './EndpointStatsChart';
import { TelemetryLogTable } from './TelemetryLogTable';
import { MiddlewareCodeViewer } from './MiddlewareCodeViewer';

export const TelemetryDashboard: React.FC = () => {
  const [logs, setLogs] = useState<TelemetryLogEntry[]>(() => generateInitialTelemetryLogs(50));
  const [isLiveStreaming, setIsLiveStreaming] = useState<boolean>(true);
  const [backendUrl, setBackendUrl] = useState<string>('https://localhost:5001');
  const [isConnectingBackend, setIsConnectingBackend] = useState<boolean>(false);
  const [backendStatus, setBackendStatus] = useState<'unknown' | 'connected' | 'offline'>('unknown');
  const [backendStatusMsg, setBackendStatusMsg] = useState<string>('');
  const [timeWindowMin, setTimeWindowMin] = useState<number>(15);

  const streamIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Auto-streaming: generuj regularne próbki
  useEffect(() => {
    if (isLiveStreaming) {
      streamIntervalRef.current = setInterval(() => {
        setLogs((prev) => {
          const newEntry = createSingleTelemetryEntry();
          const updated = [newEntry, ...prev];
          // Trzymaj do 200 ostatnich zdarzeń dla wydajności
          return updated.slice(0, 200);
        });
      }, 2500);
    } else {
      if (streamIntervalRef.current) {
        clearInterval(streamIntervalRef.current);
      }
    }

    return () => {
      if (streamIntervalRef.current) {
        clearInterval(streamIntervalRef.current);
      }
    };
  }, [isLiveStreaming]);

  // Filtrowanie logów wg wybranego okna czasowego
  const filteredLogs = useMemo(() => {
    if (timeWindowMin === 0) return logs;
    const cutoff = Date.now() - timeWindowMin * 60 * 1000;
    return logs.filter((l) => new Date(l.timestamp).getTime() >= cutoff);
  }, [logs, timeWindowMin]);

  // Wyliczane dane dla Recharts
  const timeSeriesData = useMemo(() => {
    return aggregateTelemetryTimeSeries(filteredLogs, 30);
  }, [filteredLogs]);

  const endpointStats = useMemo(() => {
    return computeEndpointStats(filteredLogs);
  }, [filteredLogs]);

  const statusBreakdown = useMemo(() => {
    return computeStatusCodeBreakdown(filteredLogs);
  }, [filteredLogs]);

  const summary = useMemo(() => {
    return computeTelemetrySummary(filteredLogs);
  }, [filteredLogs]);

  // Akcje symulacji
  const handleSimulateBurst = () => {
    const newBatch: TelemetryLogEntry[] = [];
    const now = Date.now();
    for (let i = 0; i < 10; i++) {
      const entryTime = new Date(now - (9 - i) * 800);
      newBatch.push(createSingleTelemetryEntry(entryTime, false, false));
    }
    setLogs((prev) => [...newBatch, ...prev].slice(0, 200));
  };

  const handleSimulateError = () => {
    const errorEntry = createSingleTelemetryEntry(new Date(), true, false);
    setLogs((prev) => [errorEntry, ...prev].slice(0, 200));
  };

  const handleSimulateSpike = () => {
    const spikeEntry = createSingleTelemetryEntry(new Date(), false, true);
    setLogs((prev) => [spikeEntry, ...prev].slice(0, 200));
  };

  const handleClear = () => {
    setLogs([]);
  };

  // Sprawdź połączenie z lokalnym backendem .NET
  const handleCheckBackendConnection = async () => {
    setIsConnectingBackend(true);
    setBackendStatus('unknown');
    setBackendStatusMsg('Sprawdzanie dostępności backendu .NET...');

    try {
      const res = await fetch(`${backendUrl}/.well-known/openid-configuration`, {
        method: 'GET',
        headers: { Accept: 'application/json' }
      });

      if (res.ok) {
        setBackendStatus('connected');
        setBackendStatusMsg(`Połączono pomyślnie z ${backendUrl} (HTTP ${res.status}). Serwis Quorum działa!`);
      } else {
        setBackendStatus('connected');
        setBackendStatusMsg(`Serwer odpowiada (${res.status} ${res.statusText}), jednak endpoint discovery zwrócił status nieoczekiwany.`);
      }
    } catch (err: unknown) {
      setBackendStatus('offline');
      const msg = err instanceof Error ? err.message : String(err);
      setBackendStatusMsg(
        `Nie można połączyć się z ${backendUrl}. Uruchom projekt: 'dotnet run --project Quorum.Backend' (lub sprawdź certyfikat deweloperski HTTPS).`
      );
    } finally {
      setIsConnectingBackend(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Header & Controls Card */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 shadow-sm">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-4 border-b border-slate-800">
          <div>
            <div className="flex items-center gap-2 text-emerald-400 text-xs font-semibold uppercase tracking-wider mb-1">
              <Activity className="w-4 h-4 text-emerald-400 animate-pulse" />
              OpenTelemetry Telemetry Observer (.NET 10)
            </div>
            <h2 className="text-xl font-bold text-white">
              Wizualizacja Telemetrii i Metryk Potoku Middleware
            </h2>
            <p className="text-sm text-slate-400 mt-1 max-w-3xl">
              Monitorowanie opóźnień żądań HTTP (Latency p50/p95), współczynnika błędów (Error Rates) oraz rozproszonych śladów W3C generowanych przez komponent <code className="text-emerald-300">OpenTelemetryMiddleware</code> w backendzie Quorum.
            </p>
          </div>

          {/* Quick Simulation Toggles */}
          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={() => setIsLiveStreaming(!isLiveStreaming)}
              className={`flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-semibold transition-all cursor-pointer border ${
                isLiveStreaming
                  ? 'bg-emerald-600 text-white border-emerald-500 shadow-md shadow-emerald-900/30'
                  : 'bg-slate-800 text-slate-300 border-slate-700 hover:bg-slate-700'
              }`}
            >
              {isLiveStreaming ? (
                <>
                  <Pause className="w-3.5 h-3.5" />
                  <span>Strumień Live (Włączony)</span>
                </>
              ) : (
                <>
                  <Play className="w-3.5 h-3.5" />
                  <span>Wznów Strumień Live</span>
                </>
              )}
            </button>

            <button
              onClick={handleSimulateBurst}
              className="flex items-center gap-1.5 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl text-xs font-medium transition-colors cursor-pointer border border-slate-700"
              title="Generuje serię 10 kolejnych żądań HTTP"
            >
              <PlusCircle className="w-3.5 h-3.5 text-blue-400" />
              <span>Ruch (+10 Req)</span>
            </button>

            <button
              onClick={handleSimulateError}
              className="flex items-center gap-1.5 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-rose-300 rounded-xl text-xs font-medium transition-colors cursor-pointer border border-rose-900/40"
              title="Wymusza wygenerowanie błędu HTTP 400/401/500"
            >
              <AlertTriangle className="w-3.5 h-3.5 text-rose-400" />
              <span>Symuluj Błąd</span>
            </button>

            <button
              onClick={handleSimulateSpike}
              className="flex items-center gap-1.5 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-amber-300 rounded-xl text-xs font-medium transition-colors cursor-pointer border border-amber-900/40"
              title="Wymusza nagły skok opóźnienia do ~400ms"
            >
              <Flame className="w-3.5 h-3.5 text-amber-400" />
              <span>Pik Latencji</span>
            </button>

            <button
              onClick={handleClear}
              className="p-2 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-rose-400 rounded-xl transition-colors cursor-pointer border border-slate-700"
              title="Wyczyść zebrane metryki"
            >
              <Trash2 className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Status Bar & Time Filter */}
        <div className="pt-4 flex flex-col md:flex-row md:items-center justify-between gap-4 text-xs">
          {/* Backend Connection Check */}
          <div className="flex items-center gap-2 flex-1 max-w-xl">
            <span className="font-semibold text-slate-400 shrink-0 flex items-center gap-1.5">
              <Server className="w-3.5 h-3.5 text-slate-400" /> Host .NET:
            </span>
            <input
              type="text"
              value={backendUrl}
              onChange={(e) => setBackendUrl(e.target.value)}
              className="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-white font-mono text-xs w-56 outline-none focus:border-emerald-500"
              placeholder="https://localhost:5001"
            />
            <button
              onClick={handleCheckBackendConnection}
              disabled={isConnectingBackend}
              className="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg transition-colors cursor-pointer border border-slate-700 flex items-center gap-1"
            >
              <RefreshCw className={`w-3 h-3 ${isConnectingBackend ? 'animate-spin' : ''}`} />
              <span>Sprawdź status</span>
            </button>

            {backendStatus === 'connected' && (
              <span className="flex items-center gap-1 text-emerald-400 font-medium">
                <CheckCircle2 className="w-3.5 h-3.5" /> Online
              </span>
            )}
            {backendStatus === 'offline' && (
              <span className="flex items-center gap-1 text-amber-400 font-medium">
                <Radio className="w-3.5 h-3.5" /> Mock Stream
              </span>
            )}
          </div>

          {/* Time Window Buttons */}
          <div className="flex items-center gap-1.5">
            <span className="text-slate-500 mr-1 flex items-center gap-1">
              <Clock className="w-3 h-3" /> Okno:
            </span>
            {[
              { label: '5 min', value: 5 },
              { label: '15 min', value: 15 },
              { label: '30 min', value: 30 },
              { label: 'Wszystkie', value: 0 }
            ].map((tw) => (
              <button
                key={tw.value}
                onClick={() => setTimeWindowMin(tw.value)}
                className={`px-2.5 py-1 rounded-lg transition-colors cursor-pointer text-xs ${
                  timeWindowMin === tw.value
                    ? 'bg-emerald-600/20 text-emerald-300 border border-emerald-500/40 font-semibold'
                    : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'
                }`}
              >
                {tw.label}
              </button>
            ))}
          </div>
        </div>

        {backendStatusMsg && (
          <div className="mt-3 text-[11px] font-mono p-2 rounded-lg bg-slate-950 border border-slate-800 text-slate-400">
            {backendStatusMsg}
          </div>
        )}
      </div>

      {/* KPI Cards Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Latency KPI */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-semibold">
            <span>Średnie Opóźnienie (Avg)</span>
            <Activity className="w-4 h-4 text-emerald-400" />
          </div>
          <div className="my-2">
            <div className="text-2xl font-bold font-mono text-white flex items-baseline gap-1.5">
              <span>{summary.avgLatencyMs}</span>
              <span className="text-xs text-slate-400 font-normal">ms</span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono mt-1">
              P95: <span className="text-purple-400 font-bold">{summary.p95LatencyMs} ms</span> | P99: {summary.p99LatencyMs} ms
            </div>
          </div>
          <div className="text-[11px] text-slate-500 pt-2 border-t border-slate-800/80 flex items-center justify-between">
            <span>Próg SLA: 150 ms</span>
            <span className={summary.avgLatencyMs < 100 ? 'text-emerald-400' : 'text-amber-400'}>
              {summary.avgLatencyMs < 100 ? 'W normie' : 'Podwyższone'}
            </span>
          </div>
        </div>

        {/* Error Rate KPI */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-semibold">
            <span>Współczynnik Błędów (Error Rate)</span>
            <AlertTriangle className="w-4 h-4 text-rose-400" />
          </div>
          <div className="my-2">
            <div className="text-2xl font-bold font-mono text-white flex items-baseline gap-1.5">
              <span className={summary.errorRatePercent > 5 ? 'text-rose-400' : 'text-emerald-400'}>
                {summary.errorRatePercent}%
              </span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono mt-1">
              Łącznie błędów: <span className="text-rose-400 font-bold">{summary.totalErrors}</span> / {summary.totalRequests}
            </div>
          </div>
          <div className="text-[11px] text-slate-500 pt-2 border-t border-slate-800/80 flex items-center justify-between">
            <span>Limit SLA: &lt; 5.0%</span>
            <span className={summary.errorRatePercent <= 5 ? 'text-emerald-400' : 'text-rose-400 font-bold'}>
              {summary.errorRatePercent <= 5 ? 'Zgodne ze standardem' : 'Przekroczenie SLA'}
            </span>
          </div>
        </div>

        {/* Throughput KPI */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-semibold">
            <span>Przepustowość (Throughput)</span>
            <Zap className="w-4 h-4 text-blue-400" />
          </div>
          <div className="my-2">
            <div className="text-2xl font-bold font-mono text-white flex items-baseline gap-1.5">
              <span>{summary.requestsPerMinute}</span>
              <span className="text-xs text-slate-400 font-normal">req / min</span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono mt-1">
              Zarejestrowane żądania: <span className="text-blue-400 font-bold">{summary.totalRequests}</span>
            </div>
          </div>
          <div className="text-[11px] text-slate-500 pt-2 border-t border-slate-800/80 flex items-center justify-between">
            <span>Ostatnie okno czasowe</span>
            <span className="text-slate-400 font-mono">{timeWindowMin ? `${timeWindowMin} min` : 'Wszystkie'}</span>
          </div>
        </div>

        {/* OpenTelemetry W3C Traces KPI */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-semibold">
            <span>Ślady OpenTelemetry (W3C Traces)</span>
            <ShieldCheck className="w-4 h-4 text-indigo-400" />
          </div>
          <div className="my-2">
            <div className="text-2xl font-bold font-mono text-indigo-300 flex items-baseline gap-1.5">
              <span>{summary.activeTracesCount}</span>
              <span className="text-xs text-slate-400 font-normal">spans</span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono mt-1">
              Standard: <span className="text-slate-200">W3C traceparent (RFC)</span>
            </div>
          </div>
          <div className="text-[11px] text-slate-500 pt-2 border-t border-slate-800/80 flex items-center justify-between">
            <span>ActivitySource</span>
            <span className="text-emerald-400 font-mono">Quorum.Backend</span>
          </div>
        </div>
      </div>

      {/* Primary Graphs Grid (Recharts) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <LatencyChart data={timeSeriesData} slaThresholdMs={150} />
        <ErrorRateChart data={timeSeriesData} maxAcceptableErrorRatePercent={5.0} />
      </div>

      {/* Endpoint Comparison & HTTP Status Breakdown */}
      <EndpointStatsChart endpointStats={endpointStats} statusBreakdown={statusBreakdown} />

      {/* Telemetry Activity Log Table */}
      <TelemetryLogTable logs={filteredLogs} onClearLogs={handleClear} />

      {/* Source Code Viewer for OpenTelemetryMiddleware.cs */}
      <MiddlewareCodeViewer />
    </div>
  );
};
