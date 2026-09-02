export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'OPTIONS' | 'HEAD';

export interface TelemetryLogEntry {
  id: string;
  timestamp: string; // ISO string
  timeFormatted: string; // HH:mm:ss
  traceId: string; // W3C 32-hex
  spanId: string; // W3C 16-hex
  method: HttpMethod;
  path: string;
  statusCode: number;
  durationMs: number;
  isError: boolean;
  errorMessage?: string;
  errorType?: 'validation_error' | 'unauthorized' | 'invalid_grant' | 'server_error' | 'not_found';
  clientId?: string;
  clientIp: string;
  activityTags: Record<string, string | number | boolean>;
}

export interface TelemetryTimeSeriesPoint {
  time: string; // HH:mm:ss or mm:ss
  timestamp: number;
  avgLatency: number;
  p95Latency: number;
  minLatency: number;
  maxLatency: number;
  totalRequests: number;
  errorCount: number;
  errorRate: number; // 0 - 100 %
  successCount: number;
}

export interface EndpointStat {
  endpoint: string;
  avgLatency: number;
  p95Latency: number;
  requestCount: number;
  errorCount: number;
  errorRate: number;
}

export interface StatusCodeBreakdown {
  status: string;
  code: number;
  count: number;
  percentage: number;
  color: string;
}

export interface TelemetrySummary {
  totalRequests: number;
  avgLatencyMs: number;
  p95LatencyMs: number;
  p99LatencyMs: number;
  errorRatePercent: number;
  totalErrors: number;
  requestsPerMinute: number;
  activeTracesCount: number;
}
