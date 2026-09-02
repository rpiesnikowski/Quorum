using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Telemetry;

public partial class TelemetryDashboard : ComponentBase
{
    [Inject]
    public IAdminTelemetryStore TelemetryStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    protected bool isLoading = true;
    protected TelemetryOverviewModel? overview;
    protected List<MetricTimeSeriesPoint> timeSeriesPoints = new();

    // Traces
    protected IEnumerable<GatewayTraceSpanModel> traces = new List<GatewayTraceSpanModel>();
    protected int totalTracesCount;
    protected string traceSearch = string.Empty;
    protected string traceStatusFilter = "All";
    protected List<string> statusOptions = new() { "All", "Ok", "Error" };
    protected GatewayTraceSpanModel? selectedTrace;
    private int currentTracePage = 1;

    // Logs
    protected IEnumerable<TelemetryLogEntryModel> logs = new List<TelemetryLogEntryModel>();
    protected int totalLogsCount;
    protected string logSearch = string.Empty;
    protected string logLevelFilter = "All";
    protected List<string> logLevelOptions = new() { "All", "Information", "Warning", "Error" };
    private int currentLogPage = 1;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    protected async Task LoadDataAsync()
    {
        isLoading = true;
        try
        {
            overview = await TelemetryStore.GetTelemetryOverviewAsync();
            timeSeriesPoints = await TelemetryStore.GetGatewayMetricsTimeSeriesAsync(TimeSpan.FromMinutes(30));
            await LoadTracesAsync();
            await LoadLogsAsync();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Błąd pobierania telemetrii",
                Detail = ex.Message,
                Duration = 5000
            });
        }
        finally
        {
            isLoading = false;
        }
    }

    protected async Task OnTracesLoadData(LoadDataArgs args)
    {
        currentTracePage = (args.Skip ?? 0) / (args.Top ?? 12) + 1;
        await LoadTracesAsync();
    }

    protected async Task LoadTracesAsync()
    {
        var result = await TelemetryStore.GetRecentTracesAsync(
            search: traceSearch,
            statusFilter: traceStatusFilter,
            page: currentTracePage,
            pageSize: 12);

        traces = result.Items;
        totalTracesCount = result.TotalCount;
        StateHasChanged();
    }

    protected async Task OnTraceSearchChanged(string? value)
    {
        traceSearch = value ?? string.Empty;
        currentTracePage = 1;
        await LoadTracesAsync();
    }

    protected void ShowTraceDetails(GatewayTraceSpanModel trace)
    {
        selectedTrace = trace;
    }

    protected async Task OnLogsLoadData(LoadDataArgs args)
    {
        currentLogPage = (args.Skip ?? 0) / (args.Top ?? 15) + 1;
        await LoadLogsAsync();
    }

    protected async Task LoadLogsAsync()
    {
        var result = await TelemetryStore.GetRecentLogsAsync(
            search: logSearch,
            level: logLevelFilter,
            page: currentLogPage,
            pageSize: 15);

        logs = result.Items;
        totalLogsCount = result.TotalCount;
        StateHasChanged();
    }

    protected async Task OnLogSearchChanged(string? value)
    {
        logSearch = value ?? string.Empty;
        currentLogPage = 1;
        await LoadLogsAsync();
    }

    protected async Task FilterTraceFromLog(string traceId)
    {
        traceSearch = traceId;
        currentTracePage = 1;
        await LoadTracesAsync();

        var match = await TelemetryStore.GetTraceDetailsAsync(traceId);
        if (match != null)
        {
            selectedTrace = match;
        }

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Filtrowanie wg TraceId",
            Detail = $"Przefiltrowano ślady dla identyfikatora: {traceId}",
            Duration = 3000
        });
    }

    protected async Task ClearBufferAsync()
    {
        await TelemetryStore.ClearTelemetryBufferAsync();
        selectedTrace = null;
        await LoadDataAsync();

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Bufor wyczyszczony",
            Detail = "Zresetowano lokalne bufory telemetrii i śladów.",
            Duration = 3000
        });
    }

    protected async Task OpenAspireDashboard()
    {
        var url = overview?.AspireDashboardUrl ?? "http://localhost:15001";
        await JSRuntime.InvokeVoidAsync("open", url, "_blank");
    }

    protected string GetMethodBadgeClass(string method) => method.ToUpperInvariant() switch
    {
        "GET" => "bg-success text-white",
        "POST" => "bg-primary text-white",
        "PUT" => "bg-warning text-dark",
        "PATCH" => "bg-info text-dark",
        "DELETE" => "bg-danger text-white",
        _ => "bg-secondary text-white"
    };

    protected string GetStatusBadgeClass(int? statusCode) => statusCode switch
    {
        >= 200 and < 300 => "bg-success text-white",
        >= 300 and < 400 => "bg-info text-white",
        >= 400 and < 500 => "bg-warning text-dark",
        >= 500 => "bg-danger text-white",
        _ => "bg-secondary text-white"
    };

    protected string GetLogLevelBadgeClass(string level) => level switch
    {
        "Information" => "bg-info text-white",
        "Warning" => "bg-warning text-dark",
        "Error" => "bg-danger text-white",
        "Critical" => "bg-dark text-white",
        _ => "bg-secondary text-white"
    };
}
