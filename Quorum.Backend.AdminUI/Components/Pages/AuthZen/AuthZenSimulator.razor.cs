using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;
using Quorum.Backend.EntityFramework.AuthZen;
using Radzen;

namespace Quorum.Backend.AdminUI.Components.Pages.AuthZen;

public partial class AuthZenSimulator : ComponentBase
{
    [Inject]
    public IAdminUserStore UserStore { get; set; } = default!;

    [Inject]
    public IHttpClientFactory HttpClientFactory { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    protected AuthZenEvaluationRequest evalRequest = new();
    protected AuthZenEvaluationResponse? evalResult;
    protected bool isEvaluating = false;
    protected long executionTimeMs = 0;
    protected string requestJson = string.Empty;
    protected string responseJson = string.Empty;

    protected string? selectedUserId;
    protected List<UserDropdownItem> availableUsers = new();
    protected UserAdminModel? pipData;

    protected override async Task OnInitializedAsync()
    {
        ResetRequest();
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var result = await UserStore.GetUsersAsync(page: 1, pageSize: 100);
            availableUsers = result.Items.Select(u => new UserDropdownItem
            {
                Id = u.Id,
                DisplayName = $"{u.UserName} ({u.Email}) [{(u.Roles.Count > 0 ? string.Join(", ", u.Roles) : "brak ról")}]"
            }).ToList();
        }
        catch (Exception ex)
        {
            // Opcjonalne ładowanie listy użytkowników
        }
    }

    protected void ResetRequest()
    {
        evalRequest = new AuthZenEvaluationRequest
        {
            Subject = new AuthZenSubject
            {
                Type = "user",
                Id = "user-123"
            },
            Action = new AuthZenAction
            {
                Name = "read"
            },
            Resource = new AuthZenResource
            {
                Type = "route",
                Id = "/api/v1/orders"
            }
        };

        evalResult = null;
        pipData = null;
        requestJson = string.Empty;
        responseJson = string.Empty;
    }

    protected async Task OnUserSelectionChanged(object? value)
    {
        if (value is string userId && !string.IsNullOrWhiteSpace(userId))
        {
            evalRequest.Subject.Id = userId;
            evalRequest.Subject.Type = "user";

            try
            {
                pipData = await UserStore.GetUserByIdAsync(userId);
            }
            catch
            {
                pipData = null;
            }
        }
        else
        {
            pipData = null;
        }
    }

    protected void ApplyReadPreset()
    {
        evalRequest.Action.Name = "read";
        evalRequest.Resource.Type = "route";
        evalRequest.Resource.Id = "/api/v1/orders";
    }

    protected void ApplyWritePreset()
    {
        evalRequest.Action.Name = "write";
        evalRequest.Resource.Type = "route";
        evalRequest.Resource.Id = "/api/v1/orders";
    }

    protected void ApplyAdminPreset()
    {
        evalRequest.Action.Name = "GET";
        evalRequest.Resource.Type = "route";
        evalRequest.Resource.Id = "/admin";
    }

    protected void ApplyGatewayPreset()
    {
        evalRequest.Action.Name = "GET";
        evalRequest.Resource.Type = "route";
        evalRequest.Resource.Id = "/api/v1/data";
    }

    protected async Task EvaluateAsync()
    {
        isEvaluating = true;
        var sw = Stopwatch.StartNew();

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            requestJson = JsonSerializer.Serialize(evalRequest, options);

            // Jeśli nie pobrano jeszcze danych PIP dla podmiotu, spróbuj pobrać
            if (!string.IsNullOrWhiteSpace(evalRequest.Subject.Id))
            {
                try
                {
                    pipData = await UserStore.GetUserByIdAsync(evalRequest.Subject.Id);
                }
                catch
                {
                    pipData = null;
                }
            }

            var client = HttpClientFactory.CreateClient();
            var baseUrl = NavigationManager.BaseUri.TrimEnd('/');
            var endpoint = $"{baseUrl}/authzen/evaluation/v1/evaluations";

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync(endpoint, evalRequest);
            }
            catch
            {
                // Fallback do portu 5000 jeśli BaseUri to port UI (5001)
                var fallbackEndpoint = "http://localhost:5000/authzen/evaluation/v1/evaluations";
                response = await client.PostAsJsonAsync(fallbackEndpoint, evalRequest);
            }

            sw.Stop();
            executionTimeMs = sw.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                evalResult = await response.Content.ReadFromJsonAsync<AuthZenEvaluationResponse>();
                responseJson = JsonSerializer.Serialize(evalResult, options);

                NotificationService.Notify(
                    evalResult?.Decision == true ? NotificationSeverity.Success : NotificationSeverity.Warning,
                    evalResult?.Decision == true ? "Decyzja: Permit (Zezwolono)" : "Decyzja: Deny (Odmówiono)",
                    $"Ewaluacja zakończona w {executionTimeMs} ms.");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                NotificationService.Notify(NotificationSeverity.Error, "Błąd PDP API", $"Status: {response.StatusCode} - {err}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            executionTimeMs = sw.ElapsedMilliseconds;
            NotificationService.Notify(NotificationSeverity.Error, "Błąd połączenia z PDP", ex.Message);
        }
        finally
        {
            isEvaluating = false;
        }
    }

    public class UserDropdownItem
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
