using Microsoft.AspNetCore.Components;
using Radzen;
using Quorum.Backend.AdminUI.Models;
using Quorum.Backend.AdminUI.Services.Interfaces;

namespace Quorum.Backend.AdminUI.Components.Pages.Users;

public partial class UserEdit : ComponentBase
{
    [Parameter]
    public string? Id { get; set; }

    [Inject]
    public IAdminUserStore UserStore { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    private bool IsNew => string.IsNullOrEmpty(Id);
    private UserAdminModel model = new();
    private List<string> availableRoles = new() { "Admin", "User", "Manager", "Developer" };
    private bool isSubmitting = false;

    protected override async Task OnInitializedAsync()
    {
        availableRoles = await UserStore.GetAllRolesAsync();

        if (!IsNew && !string.IsNullOrEmpty(Id))
        {
            var user = await UserStore.GetUserByIdAsync(Id);
            if (user != null)
            {
                model = user;
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Błąd", "Nie znaleziono wskazanego użytkownika.");
                NavigationManager.NavigateTo("admin/users");
            }
        }
        else
        {
            model = new UserAdminModel
            {
                EmailConfirmed = true,
                LockoutEnabled = true,
                Roles = new List<string> { "User" }
            };
        }
    }

    private async Task HandleSubmitAsync()
    {
        isSubmitting = true;
        try
        {
            if (IsNew)
            {
                var result = await UserStore.CreateUserAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", $"Pomyślnie utworzono użytkownika '{model.UserName}'.");
                    NavigationManager.NavigateTo("admin/users");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd tworzenia", result.Error ?? "Wystąpił błąd.");
                }
            }
            else
            {
                var result = await UserStore.UpdateUserAsync(model);
                if (result.Success)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Sukces", "Zaktualizowano dane użytkownika.");
                    NavigationManager.NavigateTo("admin/users");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Błąd aktualizacji", result.Error ?? "Wystąpił błąd.");
                }
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
