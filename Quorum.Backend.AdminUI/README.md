# Quorum.Backend.AdminUI

🚀 **Nowoczesny, w 100% oparty o Blazor i Radzen Blazor pakiet NuGet (Razor Class Library) dla .NET 10.**

Zapewnia kompletny panel administracyjny z pełnym CRUD dla:
1. **Użytkowników ASP.NET Identity** (`/admin/users`) – tworzenie, edycja, zmiana hasła, role, roszczenia (claims), blokowanie/odblokowywanie konta.
2. **Klientów OAuth 2.0 / OpenID Connect** (`/admin/clients`) – SPA (PKCE), Web Apps (Authorization Code), M2M (Client Credentials), Resource Owner Password.
3. **Zakresów API (ApiScopes)** (`/admin/scopes`) – zarządzanie uprawnieniami API i mapowaniem claims.
4. **Zasobów Tożsamości (IdentityResources)** (`/admin/identityresources`) – `openid`, `profile`, `email`, `address`, `phone` i własne zasoby.
5. **Dynamicznych Federacji OIDC / SSO** (`/admin/federations`) – Google, Microsoft Entra ID, Okta, Auth0, testowanie discovery.
6. **API Gateway & Reverse Proxy** (`/admin/gateway`) – trasy HTTP, walidacja tokenów i scopes, interaktywny symulator routingu.
7. **Aktywnych Grantów i Tokenów (PersistedGrants)** (`/admin/grants`) – unieważnianie tokenów, sesji i zgód użytkowników w czasie rzeczywistym.

---

## 🏛️ Czysta Architektura i Abstrakcja

Panel administracyjny `Quorum.Backend.AdminUI` operuje w 100% na **czystych modelach dziedzinowych (Domain Models / DTO)** za pośrednictwem dedykowanych interfejsów serwisów:
- `IAdminUserStore`
- `IAdminClientStore`
- `IAdminApiScopeStore`
- `IAdminIdentityResourceStore`
- `IAdminFederationStore`
- `IAdminGatewayStore`
- `IAdminGrantStore`
- `IAdminDashboardStore`

Dzięki temu panel jest **w pełni niezależny od bazy danych** – możesz użyć gotowej implementacji Entity Framework Core (`AddQuorumAdminUIEntityFrameworkStore`) lub bez trudu podpiąć własną implementację (Dapper, MongoDB, REST API, InMemory).

---

## 📦 Instalacja i Rejestracja w `Program.cs`

```csharp
using Quorum.Backend.AdminUI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja Blazor i Radzen
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();

// Rejestracja Quorum Admin UI z domyślnym magazynem Entity Framework Core
builder.Services.AddQuorumAdminUI<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
});
builder.Services.AddQuorumAdminUIEntityFrameworkStore<ApplicationUser>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Quorum.Backend.AdminUI.Components.Pages.Dashboard).Assembly);

app.Run();
```
