using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quorum.Backend.AdminUI.Models;
using Radzen;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.Models;

namespace Quorum.Backend.AdminUI.Components.Pages.Gateway;

public partial class GatewayTester : ComponentBase
{
    [Inject]
    public IAdminGatewayStore GatewayStore { get; set; } = default!;

    [Inject]
    public IAdminApiScopeStore ApiScopeStore { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    private GatewayTestRequest testRequest = new()
    {
        HttpMethod = "GET",
        RequestUrl = "/api/v1/orders/100?details=true",
        RawHeaders = "Host: localhost:5001\nAccept: application/json\nUser-Agent: Quorum-Gateway-Simulator/1.0",
        ContentType = "application/json",
        IgnoreSslErrors = true
    };

    private GatewayTestResult? testResult;
    private List<string> httpMethods = new() { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
    private List<string> contentTypes = new() { "application/json", "application/x-www-form-urlencoded", "text/plain", "application/xml" };
    private List<string> availableScopes = new();
    private bool isEvaluating = false;
    private bool isSending = false;
    private bool showCandidateMatrix = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var scopesRes = await ApiScopeStore.GetScopesAsync(pageSize: 100);
            availableScopes = scopesRes.Items.Select(s => s.Name).Distinct().ToList();
        }
        catch
        {
            // Fallback scopes
        }

        if (availableScopes.Count == 0)
        {
            availableScopes = new List<string> { "openid", "profile", "email", "api1", "orders.read", "orders.write", "admin.system" };
        }
    }

    private void ApplyPreset(string method, string path, string body, string scopes)
    {
        testRequest.HttpMethod = method;
        testRequest.RequestUrl = path;
        testRequest.RequestBody = body;
        testRequest.ProvidedScopes = scopes.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (string.IsNullOrEmpty(testRequest.RawHeaders))
        {
            testRequest.RawHeaders = "Accept: application/json\nUser-Agent: Quorum-Gateway-Simulator/1.0";
        }
    }

    private void ApplyOrdersGetPreset() => ApplyPreset("GET", "/api/orders/123", "", "orders.read");
    private void ApplyOrderPostPreset() => ApplyPreset("POST", "/api/orders", "{\n  \"customerId\": 42,\n  \"amount\": 199.99,\n  \"currency\": \"PLN\",\n  \"items\": [\"Książka\", \"Kawa\"]\n}", "orders.write");
    private void ApplyUserPutPreset() => ApplyPreset("PUT", "/api/users/profile", "{\n  \"displayName\": \"Jan Kowalski\",\n  \"department\": \"IT Security\"\n}", "profile user.manage");
    private void ApplyDeletePreset() => ApplyPreset("DELETE", "/api/cache/flush", "", "admin.system");

    private void AppendHeader(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(testRequest.RawHeaders))
        {
            testRequest.RawHeaders = headerLine;
        }
        else
        {
            testRequest.RawHeaders = testRequest.RawHeaders.TrimEnd() + "\n" + headerLine;
        }
    }

    private void FormatJsonBody()
    {
        if (string.IsNullOrWhiteSpace(testRequest.RequestBody)) return;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(testRequest.RequestBody);
            testRequest.RequestBody = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // not valid json, ignore
        }
    }

    private async Task EvaluateRouteOnlyAsync()
    {
        isEvaluating = true;
        testRequest.ExecuteLiveRequest = false;
        try
        {
            testResult = await GatewayStore.TestRouteAsync(testRequest);
        }
        finally
        {
            isEvaluating = false;
        }
    }

    private async Task SendLiveRequestAsync()
    {
        isSending = true;
        testRequest.ExecuteLiveRequest = true;
        try
        {
            testResult = await GatewayStore.TestRouteAsync(testRequest);
        }
        finally
        {
            isSending = false;
        }
    }

    private async Task CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch
        {
            // ignore clipboard errors in sandboxed iframes
        }
    }

    private string GetMethodStyle(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "color: #15803d; background-color: #f0fdf4;",
            "POST" => "color: #0369a1; background-color: #f0f9ff;",
            "PUT" => "color: #b45309; background-color: #fffbeb;",
            "DELETE" => "color: #b91c1c; background-color: #fef2f2;",
            "PATCH" => "color: #6d28d9; background-color: #f5f3ff;",
            _ => "color: #334155; background-color: #f8fafc;"
        };
    }

    private string GetStatusBadgeClass(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "bg-success",
            >= 300 and < 400 => "bg-info text-dark",
            >= 400 and < 500 => "bg-warning text-dark",
            _ => "bg-danger"
        };
    }

    private AlertStyle GetEvaluationAlertStyle()
    {
        if (testResult?.Evaluation == null)
            return AlertStyle.Info;

        if (!testResult.Evaluation.IsMatched)
            return AlertStyle.Danger;

        return testResult.Evaluation.AuthPassed ? AlertStyle.Success : AlertStyle.Warning;
    }
}
