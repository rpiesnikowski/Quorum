namespace Quorum.Backend.AdminUI.Models;

/// <summary>
/// Podsumowanie zagregowanych metryk OpenTelemetry dla API Gateway i systemu tożsamości Quorum.
/// </summary>
public class TelemetryOverviewModel
{
    public long TotalRequests { get; set; }
    public double RequestsPerMinute { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double ErrorRatePercentage { get; set; }
    public long ActiveRequestsCount { get; set; }
    public long TotalErrorsCount { get; set; }
    public long BodyTransformationsCount { get; set; }

    public string OtlpEndpointUrl { get; set; } = "http://localhost:18889";
    public string OtlpStatus { get; set; } = "Połączono (Aspire Dashboard OTLP)";
    public string ActivitySourceName { get; set; } = "Quorum.Backend.Gateway";
    public string AspireDashboardUrl { get; set; } = "http://localhost:15001";

    public List<TelemetryRouteMetric> RoutesDistribution { get; set; } = new();
    public List<StatusCodeBucket> StatusCodeDistribution { get; set; } = new();
}

public class TelemetryRouteMetric
{
    public int RouteId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string TargetHost { get; set; } = string.Empty;
    public long RequestsCount { get; set; }
    public double AverageLatencyMs { get; set; }
    public double ErrorRate { get; set; }
}

public class StatusCodeBucket
{
    public string Category { get; set; } = string.Empty; // "2xx Sukces", "4xx Błędy klienta", "5xx Błędy serwera"
    public long Count { get; set; }
    public string Color { get; set; } = "#10b981";
}

public class MetricTimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public string TimeLabel => Timestamp.ToString("HH:mm:ss");
    public long RequestsCount { get; set; }
    public double AvgLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public long ErrorsCount { get; set; }
}

/// <summary>
/// Pojedynczy ślad rozproszony (Distributed Trace Span) zarejestrowany przez Proxy2ManyHostsMiddleware.
/// </summary>
public class GatewayTraceSpanModel
{
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
    public string OperationName { get; set; } = "Proxy2ManyHosts:Forward";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double DurationMs { get; set; }

    public string HttpMethod { get; set; } = "GET";
    public string RequestPath { get; set; } = "/";
    public string TargetUri { get; set; } = string.Empty;
    public string UpstreamHost { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string Status { get; set; } = "Ok"; // "Ok" lub "Error"
    public string? ErrorMessage { get; set; }

    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TraceEventModel> Events { get; set; } = new();
}

public class TraceEventModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double OffsetMs { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// Wpis w strukturyzowanym dzienniku logów OpenTelemetry.
/// </summary>
public class TelemetryLogEntryModel
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string LogLevel { get; set; } = "Information"; // Information, Warning, Error, Critical
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Exception { get; set; }
    public Dictionary<string, string> Scopes { get; set; } = new();
}
