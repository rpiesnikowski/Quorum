using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Quorum.Backend.Gateway.Telemetry;

/// <summary>
/// Centralny punkt definicji instrumentacji OpenTelemetry dla bramki API Gateway i middleware Proxy2ManyHostsMiddleware.
/// </summary>
public static class GatewayDiagnostics
{
    public const string ServiceName = "Quorum.Backend.Gateway";
    public const string ServiceVersion = "1.0.0";
    public const string ActivitySourceName = "Quorum.Backend.Gateway";
    public const string MeterName = "Quorum.Backend.Gateway";

    // 1. Źródło śladów rozproszonych (Distributed Tracing ActivitySource)
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, ServiceVersion);

    // 2. Miernik metryk OpenTelemetry (Metrics Meter)
    public static readonly Meter Meter = new(MeterName, ServiceVersion);

    // Instrumenty metryk
    public static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
        "quorum.gateway.requests.total",
        unit: "{requests}",
        description: "Łączna liczba żądań przetworzonych przez Proxy2ManyHostsMiddleware");

    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "quorum.gateway.request.duration",
        unit: "ms",
        description: "Czas obsługi żądania upstream w milisekundach");

    public static readonly UpDownCounter<long> ActiveRequests = Meter.CreateUpDownCounter<long>(
        "quorum.gateway.active_requests",
        unit: "{requests}",
        description: "Aktualna liczba równolegle przetwarzanych żądań proxy");

    public static readonly Counter<long> ProxyErrorsTotal = Meter.CreateCounter<long>(
        "quorum.gateway.errors.total",
        unit: "{errors}",
        description: "Liczba błędów proxy (502 Bad Gateway, 504 Timeout, błędy połączenia)");

    public static readonly Counter<long> BodyTransformationsTotal = Meter.CreateCounter<long>(
        "quorum.gateway.body_transformations.total",
        unit: "{transformations}",
        description: "Liczba transformacji treści żądania (Liquid, Regex, JSON)");

    // Stałe atrybuty OpenTelemetry
    public static class Tags
    {
        public const string HttpMethod = "http.request.method";
        public const string UrlFull = "url.full";
        public const string UrlPath = "url.path";
        public const string UrlScheme = "url.scheme";
        public const string ServerAddress = "server.address";
        public const string ServerPort = "server.port";
        public const string HttpResponseStatusCode = "http.response.status_code";

        public const string RouteId = "quorum.route.id";
        public const string RoutePattern = "quorum.route.pattern";
        public const string UpstreamUrl = "quorum.upstream.url";
        public const string UpstreamHost = "quorum.upstream.host";
        public const string UpstreamPort = "quorum.upstream.port";
        public const string UpstreamTimeoutSeconds = "quorum.upstream.timeout_seconds";
        public const string ForwardOriginalHost = "quorum.upstream.forward_original_host";

        public const string AuthRequiredScope = "quorum.auth.required_scope";
        public const string AuthUserId = "quorum.auth.user_id";
        public const string AuthUserScopes = "quorum.auth.user_scopes";

        public const string BodyTransformed = "quorum.body.transformed";
        public const string BodyTransformType = "quorum.body.transform_type";

        public const string ErrorType = "error.type";
        public const string ErrorMessage = "error.message";
    }
}
