using Quorum.Backend.AdminUI.Models;

namespace Quorum.Backend.AdminUI.Services.Interfaces;

/// <summary>
/// Interfejs dostępu do zagregowanych danych telemetrii OpenTelemetry, śladów Proxy2ManyHosts oraz logów systemowych.
/// </summary>
public interface IAdminTelemetryStore
{
    Task<TelemetryOverviewModel> GetTelemetryOverviewAsync(CancellationToken cancellationToken = default);

    Task<List<MetricTimeSeriesPoint>> GetGatewayMetricsTimeSeriesAsync(TimeSpan period, CancellationToken cancellationToken = default);

    Task<PagedResult<GatewayTraceSpanModel>> GetRecentTracesAsync(
        string? search = null,
        string? statusFilter = null,
        int page = 1,
        int pageSize = 15,
        CancellationToken cancellationToken = default);

    Task<GatewayTraceSpanModel?> GetTraceDetailsAsync(string traceId, CancellationToken cancellationToken = default);

    Task<PagedResult<TelemetryLogEntryModel>> GetRecentLogsAsync(
        string? search = null,
        string? level = null,
        string? traceId = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task ClearTelemetryBufferAsync(CancellationToken cancellationToken = default);

    void RecordTrace(GatewayTraceSpanModel span);

    void RecordLog(TelemetryLogEntryModel logEntry);
}
