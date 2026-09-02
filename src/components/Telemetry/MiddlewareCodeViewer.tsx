import React, { useState } from 'react';
import { FileCode, Copy, Check, ChevronDown, ChevronUp, Sparkles, Terminal } from 'lucide-react';

export const CSHARP_MIDDLEWARE_CODE = `using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Quorum.Backend.Middleware;

/// <summary>
/// Middleware OpenTelemetry zbierający metryki opóźnienia (latency), kody błędów
/// oraz ślady W3C TraceContext (traceparent) dla każdego żądania HTTP w Quorum.Backend.
/// </summary>
public class OpenTelemetryMiddleware
{
    private static readonly ActivitySource ActivitySource = new("Quorum.Backend.Telemetry", "10.0.0");
    private readonly RequestDelegate _next;
    private readonly ILogger<OpenTelemetryMiddleware> _logger;

    public OpenTelemetryMiddleware(RequestDelegate _next, ILogger<OpenTelemetryMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        // 1. Rozpoczęcie śladu OpenTelemetry Activity (W3C TraceContext)
        using var activity = ActivitySource.StartActivity($"HTTP {method} {path}", ActivityKind.Server);
        
        var stopwatch = Stopwatch.StartNew();
        var traceId = activity?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
        var spanId = activity?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString();

        // 2. Wstrzyknięcie nagłówka śledzenia do odpowiedzi klienta
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            _logger.LogError(ex, "[OpenTelemetry] Nieobsłużony wyjątek w pipeline podczas {Method} {Path} (Trace: {TraceId})", method, path, traceId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;
            var isError = statusCode >= 400;

            // 3. Wzbogacenie tagów OpenTelemetry
            activity?.SetTag("http.request.method", method);
            activity?.SetTag("http.route", path);
            activity?.SetTag("http.response.status_code", statusCode);
            activity?.SetTag("http.duration_ms", durationMs);
            activity?.SetTag("server.address", context.Request.Host.Host);

            // Wyciągnięcie ewentualnego ClientId (np. z OIDC token lub query)
            var clientId = context.User?.FindFirst("client_id")?.Value 
                        ?? context.Request.Query["client_id"].ToString();
            if (!string.IsNullOrEmpty(clientId))
            {
                activity?.SetTag("client.id", clientId);
            }

            // 4. Emisja strukturalnego logu telemetrycznego
            _logger.LogInformation(
                "[OTEL-METRIC] {Method} {Path} returned {StatusCode} in {DurationMs}ms (TraceId: {TraceId}, SpanId: {SpanId})",
                method, path, statusCode, durationMs, traceId, spanId);

            // 5. Opcjonalny zapis w buforze pamięci dla endpointu /api/telemetry
            TelemetryBuffer.Record(new TelemetryEntry(
                traceId, spanId, method, path, statusCode, durationMs, isError, clientId, DateTime.UtcNow));
        }
    }
}

public static class OpenTelemetryMiddlewareExtensions
{
    public static IApplicationBuilder UseOpenTelemetryLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<OpenTelemetryMiddleware>();
    }
}`;

export const MiddlewareCodeViewer: React.FC = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(CSHARP_MIDDLEWARE_CODE);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-sm">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="w-full p-4 sm:p-5 flex items-center justify-between text-left hover:bg-slate-800/40 transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-indigo-500/10 border border-indigo-500/30 flex items-center justify-center text-indigo-400">
            <FileCode className="w-4 h-4" />
          </div>
          <div>
            <h4 className="text-sm font-bold text-white flex items-center gap-2">
              Kod Źródłowy: OpenTelemetryMiddleware.cs (.NET 10)
            </h4>
            <p className="text-xs text-slate-400">
              Implementacja middleware w ASP.NET Core rejestrująca pomiary czasowe i W3C TraceContext
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 text-slate-400">
          <span className="text-xs hidden sm:inline font-mono text-indigo-300">
            ActivitySource + Stopwatch
          </span>
          {isOpen ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
        </div>
      </button>

      {isOpen && (
        <div className="border-t border-slate-800 p-4 bg-slate-950 flex flex-col gap-3">
          <div className="flex items-center justify-between text-xs text-slate-400">
            <span className="font-mono">Quorum.Backend/Middleware/OpenTelemetryMiddleware.cs</span>
            <button
              onClick={handleCopy}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg transition-colors cursor-pointer border border-slate-700"
            >
              {copied ? (
                <>
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span>Skopiowano C#</span>
                </>
              ) : (
                <>
                  <Copy className="w-3.5 h-3.5" />
                  <span>Kopiuj kod C#</span>
                </>
              )}
            </button>
          </div>

          <pre className="p-4 bg-slate-900/90 rounded-xl font-mono text-xs text-slate-200 overflow-x-auto border border-slate-800/80 leading-relaxed">
            {CSHARP_MIDDLEWARE_CODE}
          </pre>
        </div>
      )}
    </div>
  );
};
