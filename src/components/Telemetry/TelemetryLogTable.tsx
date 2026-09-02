import React, { useState } from 'react';
import {
  ListFilter,
  Search,
  ExternalLink,
  Copy,
  Check,
  ChevronDown,
  ChevronUp,
  AlertCircle,
  Clock,
  Fingerprint,
  Layers,
  Terminal,
  X
} from 'lucide-react';
import { TelemetryLogEntry } from '../../types/telemetry';

interface TelemetryLogTableProps {
  logs: TelemetryLogEntry[];
  onClearLogs: () => void;
}

export const TelemetryLogTable: React.FC<TelemetryLogTableProps> = ({
  logs,
  onClearLogs
}) => {
  const [filterStatus, setFilterStatus] = useState<'all' | 'errors' | 'success'>('all');
  const [filterEndpoint, setFilterEndpoint] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedLog, setSelectedLog] = useState<TelemetryLogEntry | null>(null);
  const [copiedTraceId, setCopiedTraceId] = useState<string | null>(null);

  const endpoints = Array.from(new Set(logs.map((l) => l.path)));

  const filteredLogs = logs.filter((log) => {
    if (filterStatus === 'errors' && !log.isError) return false;
    if (filterStatus === 'success' && log.isError) return false;
    if (filterEndpoint !== 'all' && log.path !== filterEndpoint) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      return (
        log.path.toLowerCase().includes(q) ||
        log.traceId.toLowerCase().includes(q) ||
        (log.clientId && log.clientId.toLowerCase().includes(q)) ||
        (log.errorMessage && log.errorMessage.toLowerCase().includes(q))
      );
    }
    return true;
  });

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedTraceId(id);
    setTimeout(() => setCopiedTraceId(null), 2000);
  };

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 flex flex-col gap-4 shadow-sm">
      {/* Table Header & Controls */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 pb-3 border-b border-slate-800">
        <div>
          <h3 className="text-base font-bold text-white flex items-center gap-2">
            <Terminal className="w-4 h-4 text-emerald-400" />
            <span>Dziennik Zdarzeń Middleware (OpenTelemetry Activity Logs)</span>
            <span className="px-2 py-0.5 rounded-full text-xs bg-slate-800 text-slate-300 font-mono">
              {filteredLogs.length} / {logs.length}
            </span>
          </h3>
          <p className="text-xs text-slate-400">
            Szczegółowe ślady W3C Trace Context rejestrowane podczas przetwarzania zapytań w potoku middleware
          </p>
        </div>

        <div className="flex items-center gap-2 self-start md:self-auto">
          <button
            onClick={onClearLogs}
            className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs rounded-lg transition-colors cursor-pointer border border-slate-700"
          >
            Wyczyść dziennik
          </button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-3 text-xs">
        {/* Search */}
        <div className="relative">
          <Search className="w-3.5 h-3.5 text-slate-500 absolute left-3 top-2.5" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Szukaj (ścieżka, traceId, błąd)..."
            className="w-full bg-slate-950 border border-slate-800 rounded-lg pl-8 pr-3 py-2 text-white placeholder-slate-500 outline-none focus:border-emerald-500 font-mono"
          />
        </div>

        {/* Status Filter */}
        <div>
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value as any)}
            className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white outline-none focus:border-emerald-500"
          >
            <option value="all">Wszystkie statusy (2xx, 3xx, 4xx, 5xx)</option>
            <option value="errors">Tylko Błędy (4xx / 5xx)</option>
            <option value="success">Tylko Sukcesy (2xx / 3xx)</option>
          </select>
        </div>

        {/* Endpoint Filter */}
        <div>
          <select
            value={filterEndpoint}
            onChange={(e) => setFilterEndpoint(e.target.value)}
            className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white outline-none focus:border-emerald-500 font-mono"
          >
            <option value="all">Wszystkie punkty końcowe</option>
            {endpoints.map((ep) => (
              <option key={ep} value={ep}>
                {ep}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Table of Logs */}
      <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-950">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-900/80 text-slate-400 font-mono border-b border-slate-800 text-[11px] uppercase tracking-wider">
            <tr>
              <th className="py-2.5 px-3">Czas</th>
              <th className="py-2.5 px-3">Metoda</th>
              <th className="py-2.5 px-3">Endpoint / Ścieżka</th>
              <th className="py-2.5 px-3">Kod HTTP</th>
              <th className="py-2.5 px-3">Opóźnienie</th>
              <th className="py-2.5 px-3">W3C TraceId</th>
              <th className="py-2.5 px-3">Klient</th>
              <th className="py-2.5 px-3 text-right">Szczegóły</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
            {filteredLogs.length === 0 ? (
              <tr>
                <td colSpan={8} className="py-8 text-center text-slate-500 font-sans">
                  Brak logów telemetrycznych spełniających kryteria wyszukiwania.
                </td>
              </tr>
            ) : (
              filteredLogs.slice(0, 30).map((log) => {
                const is2xx = log.statusCode >= 200 && log.statusCode < 300;
                const is3xx = log.statusCode >= 300 && log.statusCode < 400;
                const is4xx = log.statusCode >= 400 && log.statusCode < 500;
                const is5xx = log.statusCode >= 500;

                const durationColor =
                  log.durationMs < 40
                    ? 'text-emerald-400 bg-emerald-500/10 border-emerald-500/20'
                    : log.durationMs < 150
                    ? 'text-amber-300 bg-amber-500/10 border-amber-500/20'
                    : 'text-rose-400 bg-rose-500/10 border-rose-500/20';

                return (
                  <tr
                    key={log.id}
                    onClick={() => setSelectedLog(log)}
                    className="hover:bg-slate-900/60 transition-colors cursor-pointer"
                  >
                    <td className="py-2 px-3 text-slate-400 whitespace-nowrap">
                      {log.timeFormatted}
                    </td>

                    <td className="py-2 px-3 whitespace-nowrap">
                      <span
                        className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${
                          log.method === 'POST'
                            ? 'bg-purple-500/20 text-purple-300 border border-purple-500/30'
                            : 'bg-blue-500/20 text-blue-300 border border-blue-500/30'
                        }`}
                      >
                        {log.method}
                      </span>
                    </td>

                    <td className="py-2 px-3 font-semibold text-white whitespace-nowrap">
                      <div className="flex items-center gap-1.5">
                        <span>{log.path}</span>
                        {log.isError && (
                          <AlertCircle className="w-3.5 h-3.5 text-rose-400 shrink-0" />
                        )}
                      </div>
                    </td>

                    <td className="py-2 px-3 whitespace-nowrap">
                      <span
                        className={`px-2 py-0.5 rounded-full text-[11px] font-bold border ${
                          is2xx
                            ? 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30'
                            : is3xx
                            ? 'bg-blue-500/15 text-blue-400 border-blue-500/30'
                            : is4xx
                            ? 'bg-amber-500/15 text-amber-400 border-amber-500/30'
                            : 'bg-rose-500/20 text-rose-400 border-rose-500/40'
                        }`}
                      >
                        {log.statusCode}
                      </span>
                    </td>

                    <td className="py-2 px-3 whitespace-nowrap">
                      <span className={`px-2 py-0.5 rounded text-[11px] font-bold border ${durationColor}`}>
                        {log.durationMs} ms
                      </span>
                    </td>

                    <td className="py-2 px-3 text-slate-400 whitespace-nowrap font-mono text-[11px]">
                      <div className="flex items-center gap-1.5">
                        <span>{log.traceId.substring(0, 8)}...{log.traceId.substring(24)}</span>
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            handleCopy(log.traceId, log.id);
                          }}
                          className="text-slate-500 hover:text-white transition-colors cursor-pointer"
                          title="Kopiuj pełny W3C TraceId"
                        >
                          {copiedTraceId === log.id ? (
                            <Check className="w-3 h-3 text-emerald-400" />
                          ) : (
                            <Copy className="w-3 h-3" />
                          )}
                        </button>
                      </div>
                    </td>

                    <td className="py-2 px-3 text-slate-400 whitespace-nowrap text-[11px]">
                      {log.clientId ? (
                        <span className="px-1.5 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-300">
                          {log.clientId}
                        </span>
                      ) : (
                        <span className="text-slate-600">—</span>
                      )}
                    </td>

                    <td className="py-2 px-3 text-right whitespace-nowrap">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedLog(log);
                        }}
                        className="px-2 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-[11px] transition-colors cursor-pointer"
                      >
                        Span Detail
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Modal / Drawer for Selected Log Detail */}
      {selectedLog && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto p-6 flex flex-col gap-4 shadow-2xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-lg bg-emerald-500/20 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
                  <Fingerprint className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-base font-bold text-white flex items-center gap-2">
                    Szczegóły Śladu OpenTelemetry Span
                  </h4>
                  <p className="text-xs text-slate-400 font-mono">
                    W3C Trace: {selectedLog.traceId}
                  </p>
                </div>
              </div>

              <button
                onClick={() => setSelectedLog(null)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Status & Method Banner */}
            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs font-mono">
              <div>
                <span className="text-slate-500 block">Metoda HTTP:</span>
                <span className="text-white font-bold">{selectedLog.method}</span>
              </div>
              <div>
                <span className="text-slate-500 block">Status:</span>
                <span
                  className={`font-bold ${
                    selectedLog.statusCode < 400 ? 'text-emerald-400' : 'text-rose-400'
                  }`}
                >
                  {selectedLog.statusCode}
                </span>
              </div>
              <div>
                <span className="text-slate-500 block">Czas trwania:</span>
                <span className="text-amber-300 font-bold">{selectedLog.durationMs} ms</span>
              </div>
              <div>
                <span className="text-slate-500 block">Znacznik czasu:</span>
                <span className="text-slate-300">{selectedLog.timeFormatted}</span>
              </div>
            </div>

            {/* Error banner if error occurred */}
            {selectedLog.isError && (
              <div className="bg-rose-500/10 border border-rose-500/30 rounded-xl p-3 text-xs text-rose-300">
                <div className="font-bold flex items-center gap-1.5 mb-1 text-rose-400">
                  <AlertCircle className="w-4 h-4" />
                  Błąd potoku middleware ({selectedLog.errorType}):
                </div>
                <div className="font-mono bg-rose-950/40 p-2 rounded border border-rose-900/40 text-[11px] break-all">
                  {selectedLog.errorMessage}
                </div>
              </div>
            )}

            {/* OpenTelemetry Activity Tags */}
            <div>
              <div className="text-xs font-bold text-slate-300 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                <Layers className="w-3.5 h-3.5 text-blue-400" />
                Atrybuty i Tagi Śladu (ActivityTags):
              </div>
              <div className="bg-slate-950 rounded-xl border border-slate-800 p-3 flex flex-col gap-1.5 font-mono text-xs">
                {Object.entries(selectedLog.activityTags).map(([key, val]) => (
                  <div key={key} className="flex flex-col sm:flex-row sm:items-center justify-between py-1 border-b border-slate-900 last:border-0 gap-1">
                    <span className="text-slate-400">{key}:</span>
                    <span className="text-emerald-300 break-all font-semibold">{String(val)}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* Raw W3C Traceparent Header */}
            <div>
              <div className="text-xs font-bold text-slate-300 uppercase tracking-wider mb-1">
                Nagłówek W3C Distributed Tracing (traceparent):
              </div>
              <div className="bg-slate-950 p-2.5 rounded-lg border border-slate-800 flex items-center justify-between gap-2 font-mono text-xs text-slate-300">
                <code className="text-blue-300 select-all">
                  00-{selectedLog.traceId}-{selectedLog.spanId}-01
                </code>
                <button
                  onClick={() =>
                    handleCopy(`00-${selectedLog.traceId}-${selectedLog.spanId}-01`, 'traceparent')
                  }
                  className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs transition-colors cursor-pointer border border-slate-700 flex items-center gap-1"
                >
                  {copiedTraceId === 'traceparent' ? (
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
            </div>

            {/* Close button */}
            <div className="pt-3 border-t border-slate-800 flex justify-end">
              <button
                onClick={() => setSelectedLog(null)}
                className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-medium cursor-pointer transition-colors"
              >
                Zamknij
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
