import React, { useState } from 'react';
import {
  ComposedChart,
  Bar,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  ReferenceLine
} from 'recharts';
import { AlertTriangle, CheckCircle2, ShieldAlert } from 'lucide-react';
import { TelemetryTimeSeriesPoint } from '../../types/telemetry';

interface ErrorRateChartProps {
  data: TelemetryTimeSeriesPoint[];
  maxAcceptableErrorRatePercent?: number;
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
          <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
          <span>Czas okna: {label}</span>
        </div>
        <div className="grid grid-cols-2 gap-x-3 gap-y-1 mt-1 text-slate-400">
          <span className="text-rose-400 font-medium">Błędy (4xx/5xx):</span>
          <span className="text-rose-300 text-right font-bold">{point.errorCount}</span>

          <span className="text-emerald-400 font-medium">Sukcesy (2xx/3xx):</span>
          <span className="text-emerald-300 text-right font-bold">{point.successCount}</span>

          <span className="text-amber-400 font-medium">Współczynnik Błędów:</span>
          <span className="text-amber-300 text-right font-bold">{point.errorRate}%</span>

          <span className="text-slate-400">Łącznie żądań:</span>
          <span className="text-white text-right font-bold">{point.totalRequests}</span>
        </div>
      </div>
    );
  }
  return null;
};

export const ErrorRateChart: React.FC<ErrorRateChartProps> = ({
  data,
  maxAcceptableErrorRatePercent = 5.0
}) => {
  const [showErrorThreshold, setShowErrorThreshold] = useState(true);

  return (
    <div className="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col gap-4 shadow-sm">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-rose-500/10 border border-rose-500/30 flex items-center justify-center text-rose-400">
            <AlertTriangle className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-white flex items-center gap-2">
              Współczynnik Błędów i Wolumen Żądań
            </h3>
            <p className="text-xs text-slate-400">
              Liczba żądań sukces/błąd (lewa oś) oraz Error Rate % (prawa oś)
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 text-xs">
          <button
            onClick={() => setShowErrorThreshold(!showErrorThreshold)}
            className={`flex items-center gap-1 px-2.5 py-1 rounded-lg transition-colors cursor-pointer border ${
              showErrorThreshold
                ? 'bg-rose-500/20 text-rose-300 border-rose-500/40 font-medium'
                : 'bg-slate-950 text-slate-500 border-slate-800 hover:text-slate-400'
            }`}
          >
            <ShieldAlert className="w-3 h-3" />
            <span>Próg SLA ({maxAcceptableErrorRatePercent}%)</span>
          </button>
        </div>
      </div>

      <div className="h-64 w-full">
        {data.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-500 text-xs font-mono">
            Brak zarejestrowanych błędów w wybranym oknie czasowym
          </div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data} margin={{ top: 10, right: 10, left: -15, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#334155" opacity={0.5} />
              <XAxis
                dataKey="time"
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 11 }}
                tickLine={false}
              />
              <YAxis
                yAxisId="left"
                stroke="#64748b"
                tick={{ fill: '#94a3b8', fontSize: 11 }}
                tickLine={false}
                allowDecimals={false}
              />
              <YAxis
                yAxisId="right"
                orientation="right"
                stroke="#f59e0b"
                tick={{ fill: '#f59e0b', fontSize: 11 }}
                unit="%"
                domain={[0, (dataMax: number) => Math.max(Math.ceil(dataMax * 1.2), 10)]}
                tickLine={false}
              />
              <Tooltip content={<CustomTooltip />} />

              {showErrorThreshold && (
                <ReferenceLine
                  yAxisId="right"
                  y={maxAcceptableErrorRatePercent}
                  stroke="#ef4444"
                  strokeDasharray="4 4"
                  label={{
                    value: `Próg Alarmowy (${maxAcceptableErrorRatePercent}%)`,
                    fill: '#ef4444',
                    fontSize: 10,
                    position: 'top'
                  }}
                />
              )}

              <Bar
                yAxisId="left"
                dataKey="successCount"
                name="Sukces (2xx/3xx)"
                stackId="requests"
                fill="#10b981"
                radius={[0, 0, 0, 0]}
              />

              <Bar
                yAxisId="left"
                dataKey="errorCount"
                name="Błąd (4xx/5xx)"
                stackId="requests"
                fill="#f43f5e"
                radius={[4, 4, 0, 0]}
              />

              <Line
                yAxisId="right"
                type="monotone"
                dataKey="errorRate"
                name="Error Rate (%)"
                stroke="#f59e0b"
                strokeWidth={2.5}
                dot={{ r: 3, fill: '#f59e0b' }}
                activeDot={{ r: 5, fill: '#f59e0b' }}
              />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </div>

      <div className="flex items-center justify-between text-xs text-slate-400 pt-2 border-t border-slate-800/60">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
            <span className="text-slate-300 font-medium">Sukcesy</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-rose-500" />
            <span className="text-slate-300 font-medium">Błędy</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-amber-400" />
            <span className="text-slate-300 font-medium">Współczynnik Błędów (%)</span>
          </div>
        </div>
        <span className="text-[11px] text-slate-500 font-mono">
          Dwukierunkowa skala osi
        </span>
      </div>
    </div>
  );
};
