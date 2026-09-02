import { TelemetryLogEntry, TelemetryTimeSeriesPoint, EndpointStat, StatusCodeBreakdown, TelemetrySummary } from '../types/telemetry';

function randomHex(length: number): string {
  const chars = '0123456789abcdef';
  let result = '';
  for (let i = 0; i < length; i++) {
    result += chars[Math.floor(Math.random() * chars.length)];
  }
  return result;
}

export function generateW3CTraceId(): string {
  return randomHex(32);
}

export function generateW3CSpanId(): string {
  return randomHex(16);
}

const COMMON_ENDPOINTS = [
  { path: '/connect/token', method: 'POST' as const, baseMs: 45, variance: 35, client: 'frontend-spa-portal', weight: 35 },
  { path: '/connect/authorize', method: 'GET' as const, baseMs: 25, variance: 18, client: 'frontend-spa-portal', weight: 20 },
  { path: '/.well-known/openid-configuration', method: 'GET' as const, baseMs: 8, variance: 6, client: 'anonymous', weight: 15 },
  { path: '/connect/userinfo', method: 'GET' as const, baseMs: 22, variance: 14, client: 'quorum_web_client', weight: 12 },
  { path: '/Admin/Clients', method: 'GET' as const, baseMs: 65, variance: 40, client: 'admin-console', weight: 8 },
  { path: '/api/gateway/routes', method: 'GET' as const, baseMs: 14, variance: 10, client: 'quorum-gateway-proxy', weight: 10 }
];

export function createSingleTelemetryEntry(
  customTime?: Date,
  forceError = false,
  forceSpike = false
): TelemetryLogEntry {
  const now = customTime || new Date();
  const timeFormatted = now.toLocaleTimeString();

  // Wybierz endpoint na podstawie wag
  const totalWeight = COMMON_ENDPOINTS.reduce((sum, ep) => sum + ep.weight, 0);
  let rnd = Math.random() * totalWeight;
  let selectedEndpoint = COMMON_ENDPOINTS[0];
  for (const ep of COMMON_ENDPOINTS) {
    if (rnd < ep.weight) {
      selectedEndpoint = ep;
      break;
    }
    rnd -= ep.weight;
  }

  const isErr = forceError || Math.random() < 0.07; // ~7% błędów naturalnie
  let statusCode = 200;
  let errorMessage: string | undefined;
  let errorType: TelemetryLogEntry['errorType'];

  if (isErr) {
    const errRand = Math.random();
    if (selectedEndpoint.path === '/connect/token') {
      if (errRand < 0.6) {
        statusCode = 400;
        errorMessage = 'invalid_grant: The provided authorization code is expired or invalid.';
        errorType = 'invalid_grant';
      } else if (errRand < 0.85) {
        statusCode = 401;
        errorMessage = 'invalid_client: Client authentication failed (invalid client_secret).';
        errorType = 'unauthorized';
      } else {
        statusCode = 500;
        errorMessage = 'System.TimeoutException: Database query timed out connecting to Postgres replica pool.';
        errorType = 'server_error';
      }
    } else if (selectedEndpoint.path === '/connect/userinfo') {
      statusCode = 401;
      errorMessage = 'unauthorized: Bearer token has expired.';
      errorType = 'unauthorized';
    } else if (selectedEndpoint.path === '/Admin/Clients') {
      statusCode = 403;
      errorMessage = 'forbidden: User role does not satisfy policy QuorumAdminPolicy.';
      errorType = 'unauthorized';
    } else {
      statusCode = Math.random() < 0.7 ? 400 : 500;
      errorMessage = statusCode === 400 ? 'validation_error: Missing required query parameter.' : 'InternalServerError: Unexpected failure in pipeline.';
      errorType = statusCode === 400 ? 'validation_error' : 'server_error';
    }
  } else {
    if (selectedEndpoint.path === '/connect/authorize') {
      statusCode = 302; // Redirect to callback or login
    } else {
      statusCode = 200;
    }
  }

  let durationMs = selectedEndpoint.baseMs + Math.floor(Math.random() * selectedEndpoint.variance);
  if (forceSpike) {
    durationMs = Math.floor(250 + Math.random() * 400); // 250 - 650 ms spike
  } else if (isErr && statusCode === 500) {
    durationMs = Math.floor(300 + Math.random() * 200); // błąd serwera trwa dłużej
  }

  const traceId = generateW3CTraceId();
  const spanId = generateW3CSpanId();

  return {
    id: `otel_${Date.now()}_${randomHex(6)}`,
    timestamp: now.toISOString(),
    timeFormatted,
    traceId,
    spanId,
    method: selectedEndpoint.method,
    path: selectedEndpoint.path,
    statusCode,
    durationMs,
    isError: isErr,
    errorMessage,
    errorType,
    clientId: selectedEndpoint.client,
    clientIp: '127.0.0.1',
    activityTags: {
      'otel.library.name': 'Quorum.OpenTelemetry.Middleware',
      'otel.library.version': '10.0.0',
      'http.request.method': selectedEndpoint.method,
      'http.route': selectedEndpoint.path,
      'http.response.status_code': statusCode,
      'server.address': 'localhost',
      'server.port': 5001,
      'client.id': selectedEndpoint.client,
      'w3c.traceparent': `00-${traceId}-${spanId}-01`,
      'aspnetcore.environment': 'Development'
    }
  };
}

export function generateInitialTelemetryLogs(count = 50): TelemetryLogEntry[] {
  const logs: TelemetryLogEntry[] = [];
  const now = Date.now();
  const timeStepMs = 15000; // co 15 sekund w przeszłość

  for (let i = count - 1; i >= 0; i--) {
    const entryTime = new Date(now - i * timeStepMs);
    const forceSpike = i === 12 || i === 31; // sporadyczne piki opóźnienia
    const forceError = i === 8 || i === 22 || i === 39; // wybrane błędy dla urealnienia
    logs.push(createSingleTelemetryEntry(entryTime, forceError, forceSpike));
  }

  return logs;
}

// Grupowanie logów w punkty czasowe dla wykresów Recharts
export function aggregateTelemetryTimeSeries(
  logs: TelemetryLogEntry[],
  bucketSizeSeconds = 30
): TelemetryTimeSeriesPoint[] {
  if (logs.length === 0) return [];

  // Sortowanie rosnąco wg czasu
  const sorted = [...logs].sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
  
  const buckets = new Map<number, TelemetryLogEntry[]>();

  for (const log of sorted) {
    const timeMs = new Date(log.timestamp).getTime();
    const bucketKey = Math.floor(timeMs / (bucketSizeSeconds * 1000)) * (bucketSizeSeconds * 1000);
    if (!buckets.has(bucketKey)) {
      buckets.set(bucketKey, []);
    }
    buckets.get(bucketKey)!.push(log);
  }

  const result: TelemetryTimeSeriesPoint[] = [];

  buckets.forEach((entries, timestamp) => {
    const latencies = entries.map((e) => e.durationMs).sort((a, b) => a - b);
    const totalRequests = entries.length;
    const errorCount = entries.filter((e) => e.isError).length;
    const successCount = totalRequests - errorCount;
    const avgLatency = Math.round(latencies.reduce((sum, l) => sum + l, 0) / totalRequests);
    
    // P95
    const p95Index = Math.min(Math.floor(latencies.length * 0.95), latencies.length - 1);
    const p95Latency = latencies[p95Index] || avgLatency;

    const minLatency = latencies[0] || 0;
    const maxLatency = latencies[latencies.length - 1] || 0;
    const errorRate = Math.round((errorCount / totalRequests) * 1000) / 10; // procent z 1 miejscem po przecinku

    const date = new Date(timestamp);
    const time = `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`;

    result.push({
      time,
      timestamp,
      avgLatency,
      p95Latency,
      minLatency,
      maxLatency,
      totalRequests,
      errorCount,
      errorRate,
      successCount
    });
  });

  return result;
}

// Statystyki dla poszczególnych endpointów
export function computeEndpointStats(logs: TelemetryLogEntry[]): EndpointStat[] {
  const map = new Map<string, { latencies: number[]; errors: number; total: number }>();

  for (const log of logs) {
    if (!map.has(log.path)) {
      map.set(log.path, { latencies: [], errors: 0, total: 0 });
    }
    const stat = map.get(log.path)!;
    stat.latencies.push(log.durationMs);
    stat.total++;
    if (log.isError) {
      stat.errors++;
    }
  }

  const result: EndpointStat[] = [];
  map.forEach((data, endpoint) => {
    data.latencies.sort((a, b) => a - b);
    const avgLatency = Math.round(data.latencies.reduce((a, b) => a + b, 0) / data.total);
    const p95Index = Math.min(Math.floor(data.latencies.length * 0.95), data.latencies.length - 1);
    const p95Latency = data.latencies[p95Index] || avgLatency;
    const errorRate = Math.round((data.errors / data.total) * 1000) / 10;

    result.push({
      endpoint,
      avgLatency,
      p95Latency,
      requestCount: data.total,
      errorCount: data.errors,
      errorRate
    });
  });

  // Sortuj wg liczby żądań malejąco
  return result.sort((a, b) => b.requestCount - a.requestCount);
}

// Rozkład kodów stanu HTTP
export function computeStatusCodeBreakdown(logs: TelemetryLogEntry[]): StatusCodeBreakdown[] {
  if (logs.length === 0) return [];

  const counts: Record<number, number> = {};
  logs.forEach((l) => {
    counts[l.statusCode] = (counts[l.statusCode] || 0) + 1;
  });

  const colorMap: Record<number, string> = {
    200: '#10b981', // emerald
    302: '#3b82f6', // blue
    400: '#f59e0b', // amber
    401: '#f97316', // orange
    403: '#ef4444', // red
    500: '#dc2626'  // dark red
  };

  return Object.entries(counts)
    .map(([codeStr, count]) => {
      const code = parseInt(codeStr, 10);
      const percentage = Math.round((count / logs.length) * 1000) / 10;
      let status = `${code} OK`;
      if (code === 302) status = '302 Redirect';
      if (code === 400) status = '400 Bad Request';
      if (code === 401) status = '401 Unauthorized';
      if (code === 403) status = '403 Forbidden';
      if (code === 500) status = '500 Server Error';

      return {
        code,
        status,
        count,
        percentage,
        color: colorMap[code] || '#94a3b8'
      };
    })
    .sort((a, b) => b.count - a.count);
}

// Podsumowanie wskaźników KPI
export function computeTelemetrySummary(logs: TelemetryLogEntry[]): TelemetrySummary {
  if (logs.length === 0) {
    return {
      totalRequests: 0,
      avgLatencyMs: 0,
      p95LatencyMs: 0,
      p99LatencyMs: 0,
      errorRatePercent: 0,
      totalErrors: 0,
      requestsPerMinute: 0,
      activeTracesCount: 0
    };
  }

  const latencies = logs.map((l) => l.durationMs).sort((a, b) => a - b);
  const totalRequests = logs.length;
  const totalErrors = logs.filter((l) => l.isError).length;
  const avgLatencyMs = Math.round(latencies.reduce((a, b) => a + b, 0) / totalRequests);
  
  const p95Idx = Math.min(Math.floor(latencies.length * 0.95), latencies.length - 1);
  const p99Idx = Math.min(Math.floor(latencies.length * 0.99), latencies.length - 1);
  const p95LatencyMs = latencies[p95Idx] || avgLatencyMs;
  const p99LatencyMs = latencies[p99Idx] || p95LatencyMs;
  const errorRatePercent = Math.round((totalErrors / totalRequests) * 1000) / 10;

  // Obliczenie requests per minute na podstawie rozpiętości czasowej
  const minTime = Math.min(...logs.map((l) => new Date(l.timestamp).getTime()));
  const maxTime = Math.max(...logs.map((l) => new Date(l.timestamp).getTime()));
  const diffMinutes = Math.max((maxTime - minTime) / 60000, 0.5);
  const requestsPerMinute = Math.round(totalRequests / diffMinutes);

  return {
    totalRequests,
    avgLatencyMs,
    p95LatencyMs,
    p99LatencyMs,
    errorRatePercent,
    totalErrors,
    requestsPerMinute,
    activeTracesCount: totalRequests
  };
}
