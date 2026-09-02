import React from 'react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell
} from 'recharts';
import { Layers, Network, CheckCircle2, AlertCircle } from 'lucide-react';
import { EndpointStat, StatusCodeBreakdown } from '../../types/telemetry';

interface EndpointStatsChartProps {
  endpointStats: EndpointStat[];
  statusBreakdown: StatusCodeBreakdown[];
}

export const EndpointStatsChart: React.FC<EndpointStatsChartProps> = ({
  endpointStats,
  statusBreakdown
}) => {
  // Przygotuj etykiety skrócone dla czytelności na osi X
  const formattedData = endpointStats.map((item) => ({
    ...item,
    shortName: item.endpoint.replace('/.well-known', '..well-known')
  }));

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
      {/* Endpoint Latency Comparison */}
      <div className="lg:col-span-2 bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col gap-4 shadow-sm">
        <div className="flex items-center justify-between pb-3 border-b border-slate-800">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-blue-500/10 border border-blue-500/30 flex items-center justify-center text-blue-400">
              <Network className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white">
                Porównanie Latencji wg Punktów Końcowych
              </h3>
              <p className="text-xs text-slate-400">
                Średnie opóźnienie (Avg) i 95. percentyl (P95) na poszczególnych endpointach OIDC
              </p>
            </div>
          </div>
        </div>

        <div className="h-56 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={formattedData} margin={{ top: 10, right: 10, left: -15, bottom: 25 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" opacity={0.5} />
              <XAxis
                dataKey="shortName"
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 10 }}
                interval={0}
                angle={-15}
                textAnchor="end"
                tickLine={false}
              />
              <YAxis
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 11 }}
                unit="ms"
                tickLine={false}
              />
              <Tooltip
                content={({ active, payload }) => {
                  if (active && payload && payload.length) {
                    const data = payload[0].payload as EndpointStat;
                    return (
                      <div className="bg-slate-950/95 border border-slate-700/80 p-3 rounded-xl shadow-xl text-xs font-mono">
                        <div className="text-white font-bold mb-1 border-b border-slate-800 pb-1">
                          {data.endpoint}
                        </div>
                        <div className="grid grid-cols-2 gap-x-2 gap-y-1 text-slate-400 mt-1">
                          <span className="text-blue-400">Średnia (Avg):</span>
                          <span className="text-white font-semibold text-right">{data.avgLatency} ms</span>
                          <span className="text-purple-400">P95:</span>
                          <span className="text-white font-semibold text-right">{data.p95Latency} ms</span>
                          <span>Żądania:</span>
                          <span className="text-slate-300 text-right">{data.requestCount}</span>
                          <span>Błędy:</span>
                          <span className={data.errorCount > 0 ? 'text-rose-400 text-right' : 'text-slate-400 text-right'}>
                            {data.errorCount} ({data.errorRate}%)
                          </span>
                        </div>
                      </div>
                    );
                  }
                  return null;
                }}
              />
              <Bar
                dataKey="avgLatency"
                name="Średnia Latencja (ms)"
                fill="#3b82f6"
                radius={[4, 4, 0, 0]}
              />
              <Bar
                dataKey="p95Latency"
                name="P95 Latency (ms)"
                fill="#8b5cf6"
                radius={[4, 4, 0, 0]}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="flex items-center gap-4 text-xs text-slate-400 pt-2 border-t border-slate-800/60">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded bg-blue-500" />
            <span className="text-slate-300">Avg Latency (ms)</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded bg-purple-500" />
            <span className="text-slate-300">P95 Latency (ms)</span>
          </div>
        </div>
      </div>

      {/* HTTP Status Code Breakdown */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between shadow-sm">
        <div>
          <div className="flex items-center gap-2 pb-3 border-b border-slate-800 mb-4">
            <div className="w-8 h-8 rounded-lg bg-teal-500/10 border border-teal-500/30 flex items-center justify-center text-teal-400">
              <Layers className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white">
                Rozkład Kodów HTTP
              </h3>
              <p className="text-xs text-slate-400">
                Struktura odpowiedzi middleware
              </p>
            </div>
          </div>

          <div className="flex flex-col gap-2.5">
            {statusBreakdown.map((item) => {
              const is2xx = item.code >= 200 && item.code < 300;
              const is3xx = item.code >= 300 && item.code < 400;
              const is4xx = item.code >= 400 && item.code < 500;
              const is5xx = item.code >= 500;

              return (
                <div
                  key={item.code}
                  className="bg-slate-950/80 border border-slate-800/80 rounded-xl p-2.5 flex flex-col gap-1.5"
                >
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{ backgroundColor: item.color }}
                      />
                      <span className="font-mono font-semibold text-slate-200">
                        {item.status}
                      </span>
                    </div>
                    <div className="flex items-center gap-2 font-mono">
                      <span className="text-slate-400">{item.count} req</span>
                      <span className="font-bold text-white">{item.percentage}%</span>
                    </div>
                  </div>

                  {/* Progress bar */}
                  <div className="w-full bg-slate-900 h-1.5 rounded-full overflow-hidden">
                    <div
                      className="h-full rounded-full transition-all duration-500"
                      style={{
                        width: `${Math.max(item.percentage, 3)}%`,
                        backgroundColor: item.color
                      }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="pt-3 border-t border-slate-800/80 text-[11px] text-slate-500 flex items-center justify-between">
          <span>Telemetria ASP.NET Core 10</span>
          <span className="text-emerald-400 flex items-center gap-1 font-mono">
            <CheckCircle2 className="w-3 h-3" /> W3C ActivitySource
          </span>
        </div>
      </div>
    </div>
  );
};
