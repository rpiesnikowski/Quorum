import React, { useState } from 'react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  ReferenceLine
} from 'recharts';
import { Clock, ShieldAlert, Activity } from 'lucide-react';
import { TelemetryTimeSeriesPoint } from '../../types/telemetry';

interface LatencyChartProps {
  data: TelemetryTimeSeriesPoint[];
  slaThresholdMs?: number;
}

interface CustomTooltipProps {
  active?: boolean;
  payload?: Array<{
    value: number;
    dataKey: string;
    color: string;
    payload: TelemetryTimeSeriesPoint;
  }>;
  label?: string;
}

const CustomTooltip: React.FC<CustomTooltipProps> = ({ active, payload, label }) => {
  if (active && payload && payload.length) {
    const point = payload[0].payload;
    return (
      <div className="bg-slate-950/95 border border-slate-700/80 p-3 rounded-xl shadow-xl backdrop-blur-md text-xs font-mono">
        <div className="text-slate-300 font-semibold mb-1 flex items-center gap-1.5 border-b border-slate-800 pb-1">
          <Clock className="w-3.5 h-3.5 text-emerald-400" />
          <span>Czas okna: {label}</span>
        </div>
        <div className="grid grid-cols-2 gap-x-3 gap-y-1 mt-1 text-slate-400">
          <span className="text-emerald-400 font-medium">Średnia (Avg):</span>
          <span className="text-white text-right font-bold">{point.avgLatency} ms</span>
          
          <span className="text-purple-400 font-medium">P95 Latency:</span>
          <span className="text-white text-right font-bold">{point.p95Latency} ms</span>

          <span className="text-slate-400">Min / Max:</span>
          <span className="text-slate-300 text-right">{point.minLatency} / {point.maxLatency} ms</span>

          <span className="text-slate-400">Liczba żądań:</span>
          <span className="text-blue-400 text-right font-bold">{point.totalRequests}</span>
        </div>
      </div>
    );
  }
  return null;
};

export const LatencyChart: React.FC<LatencyChartProps> = ({
  data,
  slaThresholdMs = 150
}) => {
  const [showP95, setShowP95] = useState(true);
  const [showSla, setShowSla] = useState(true);

  return (
    <div className="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col gap-4 shadow-sm">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
            <Activity className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-white flex items-center gap-2">
              Opóźnienie Żądań HTTP (Request Latency)
            </h3>
            <p className="text-xs text-slate-400">
              Pomiary czasowe generowane przez <code className="text-emerald-300">OpenTelemetryMiddleware</code> (Avg & P95)
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 text-xs">
          <button
            onClick={() => setShowP95(!showP95)}
            className={`px-2.5 py-1 rounded-lg transition-colors cursor-pointer border ${
              showP95
                ? 'bg-purple-500/20 text-purple-300 border-purple-500/40 font-medium'
                : 'bg-slate-950 text-slate-500 border-slate-800 hover:text-slate-400'
            }`}
          >
            P95 Linia
          </button>
          <button
            onClick={() => setShowSla(!showSla)}
            className={`flex items-center gap-1 px-2.5 py-1 rounded-lg transition-colors cursor-pointer border ${
              showSla
                ? 'bg-amber-500/20 text-amber-300 border-amber-500/40 font-medium'
                : 'bg-slate-950 text-slate-500 border-slate-800 hover:text-slate-400'
            }`}
          >
            <ShieldAlert className="w-3 h-3" />
            <span>SLA {slaThresholdMs}ms</span>
          </button>
        </div>
      </div>

      <div className="h-64 w-full">
        {data.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-500 text-xs font-mono">
            Oczekiwanie na dane telemetryczne z potoku middleware...
          </div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 10, right: 10, left: -15, bottom: 0 }}>
              <defs>
                <linearGradient id="latencyAvgGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#10b981" stopOpacity={0.4} />
                  <stop offset="95%" stopColor="#10b981" stopOpacity={0.0} />
                </linearGradient>
                <linearGradient id="latencyP95Grad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#a855f7" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#a855f7" stopOpacity={0.0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" opacity={0.5} />
              <XAxis
                dataKey="time"
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 11 }}
                tickLine={false}
              />
              <YAxis
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 11 }}
                unit="ms"
                tickLine={false}
              />
              <Tooltip content={<CustomTooltip />} />
              
              {showSla && (
                <ReferenceLine
                  y={slaThresholdMs}
                  stroke="#f59e0b"
                  strokeDasharray="4 4"
                  label={{
                    value: `SLA Alert (${slaThresholdMs}ms)`,
                    fill: '#f59e0b',
                    fontSize: 10,
                    position: 'top'
                  }}
                />
              )}

              {showP95 && (
                <Area
                  type="monotone"
                  dataKey="p95Latency"
                  name="P95 Latency"
                  stroke="#a855f7"
                  strokeWidth={2}
                  fillOpacity={1}
                  fill="url(#latencyP95Grad)"
                />
              )}

              <Area
                type="monotone"
                dataKey="avgLatency"
                name="Średnie Opóźnienie"
                stroke="#10b981"
                strokeWidth={2}
                fillOpacity={1}
                fill="url(#latencyAvgGrad)"
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </div>

      <div className="flex items-center justify-between text-xs text-slate-400 pt-2 border-t border-slate-800/60">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
            <span className="text-slate-300 font-medium">Średnia Latencja (Avg)</span>
          </div>
          {showP95 && (
            <div className="flex items-center gap-1.5">
              <span className="w-2.5 h-2.5 rounded-full bg-purple-500" />
              <span className="text-slate-300 font-medium">95. Percentyl (P95)</span>
            </div>
          )}
        </div>
        <span className="text-[11px] text-slate-500 font-mono">
          Krok agregacji: 30s
        </span>
      </div>
    </div>
  );
};
