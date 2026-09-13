using Microsoft.AspNetCore.Components;
using Quorum.FineGrainedAuth.OpenFGA.Models;
using Quorum.FineGrainedAuth.OpenFGA.Services;
using Radzen;
using Radzen.Blazor;

namespace Quorum.FineGrainedAuth.UI.Components;

public partial class OpenFgaRuleManager : ComponentBase
{
    [Inject] public IAuthZenOpenFgaStore Store { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public DialogService DialogService { get; set; } = default!;

    protected RadzenDataGrid<AuthZenRuleDto>? grid;
    protected List<AuthZenRuleDto> rules = new();
    protected FgaServerStatus serverStatus = new();
    protected string searchTerm = string.Empty;
    protected bool isLoading;
    protected bool isSyncing;
    protected bool isSaving;
    protected bool isChecking;

    protected bool showEditModal;
    protected bool isEditing;
    protected AuthZenRuleDto currentRule = new();

    protected bool showCheckModal;
    protected string checkUser = "user:anne";
    protected string checkRelation = "reader";
    protected string checkObject = "document:roadmap_2026";
    protected FgaCheckResponse? checkResult;

    protected readonly string[] subjectTypes = { "user", "role", "group", "service" };
    protected readonly string[] actions = { "reader", "writer", "owner", "admin", "viewer", "editor", "executor" };
    protected readonly string[] resourceTypes = { "document", "route", "repo", "project", "api", "resource" };

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    protected async Task LoadDataAsync()
    {
        isLoading = true;
        try
        {
            var data = await Store.GetRulesAsync(searchTerm);
            rules = data.ToList();
            serverStatus = await Store.GetStatusAsync();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd", $"Nie udało się pobrać reguł: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    protected async Task OnSearchInput(ChangeEventArgs args)
    {
        searchTerm = args.Value?.ToString() ?? string.Empty;
        await LoadDataAsync();
    }

    protected void OpenCreateDialog()
    {
        isEditing = false;
        currentRule = new AuthZenRuleDto
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            SubjectType = "user",
            SubjectId = "anne",
            Action = "reader",
            ResourceType = "document",
            ResourceId = "roadmap_2026",
            Effect = "Permit",
            IsEnabled = true,
            Name = "Nowa reguła dostępu"
        };
        showEditModal = true;
    }

    protected void OpenEditDialog(AuthZenRuleDto rule)
    {
        isEditing = true;
        currentRule = new AuthZenRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            SubjectType = rule.SubjectType,
            SubjectId = rule.SubjectId,
            Action = rule.Action,
            ResourceType = rule.ResourceType,
            ResourceId = rule.ResourceId,
            Effect = rule.Effect,
            IsEnabled = rule.IsEnabled,
            ConditionName = rule.ConditionName,
            SyncStatus = rule.SyncStatus
        };
        showEditModal = true;
    }

    protected async Task SaveRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(currentRule.SubjectId) || string.IsNullOrWhiteSpace(currentRule.Action) || string.IsNullOrWhiteSpace(currentRule.ResourceId))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Walidacja", "Wszystkie pola podmiotu, akcji i zasobu są wymagane.");
            return;
        }

        isSaving = true;
        try
        {
            AuthZenRuleSyncResult result;
            if (isEditing)
            {
                result = await Store.UpdateRuleAsync(currentRule.Id, currentRule);
            }
            else
            {
                result = await Store.CreateRuleAsync(currentRule);
            }

            if (result.Success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Sukces", result.Message);
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Uwaga", result.Message);
            }

            showEditModal = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd zapisu", ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    protected async Task DeleteRuleAsync(AuthZenRuleDto rule)
    {
        var confirmed = await DialogService.Confirm($"Czy na pewno chcesz usunąć regułę '{rule.Name}' i wycofać relację ({rule.FgaUser} -> {rule.FgaRelation} -> {rule.FgaObject}) z OpenFGA?", "Potwierdzenie usunięcia", new ConfirmOptions { OkButtonText = "Usuń", CancelButtonText = "Anuluj" });

        if (confirmed == true)
        {
            var result = await Store.DeleteRuleAsync(rule.Id);
            NotificationService.Notify(result.Success ? NotificationSeverity.Success : NotificationSeverity.Warning, "Usunięcie", result.Message);
            await LoadDataAsync();
        }
    }

    protected async Task SyncRuleAsync(AuthZenRuleDto rule)
    {
        var result = await Store.SyncRuleAsync(rule.Id);
        NotificationService.Notify(result.Success ? NotificationSeverity.Success : NotificationSeverity.Warning, "Synchronizacja OpenFGA", result.Message);
        await LoadDataAsync();
    }

    protected async Task SyncAllAsync()
    {
        isSyncing = true;
        try
        {
            var result = await Store.SyncAllAsync();
            NotificationService.Notify(result.Success ? NotificationSeverity.Success : NotificationSeverity.Warning, "Synchronizacja hurtowa", result.Message);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Błąd synchronizacji", ex.Message);
        }
        finally
        {
            isSyncing = false;
        }
    }

    protected async Task PerformCheckAsync()
    {
        isChecking = true;
        try
        {
            checkResult = await Store.CheckAccessAsync(checkUser, checkRelation, checkObject);
        }
        catch (Exception ex)
        {
            checkResult = new FgaCheckResponse
            {
                Allowed = false,
                Resolution = $"Błąd podczas ewaluacji check: {ex.Message}"
            };
        }
        finally
        {
            isChecking = false;
        }
    }
}
