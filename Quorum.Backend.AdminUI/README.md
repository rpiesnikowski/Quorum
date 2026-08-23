# Quorum.Backend.AdminUI

Kompletny, modułowy panel administracyjny Razor Class Library (RCL) / pakiet NuGet dla **Open.IdentityServer** oraz **ASP.NET Core Identity** w środowisku **.NET 10**.

## ✨ Główne Możliwości
- 🏢 **Zarządzanie Klientami OAuth2 / OIDC (Clients):** Pełny formularz konfiguracyjny (Basic, Secrets z hashowaniem SHA-256, Grant Types & PKCE, Redirect/PostLogout URIs, CORS, Allowed Scopes, Token Lifetimes, Client Claims).
- 🔑 **Zasoby Tożsamości (IdentityResources):** Dodawanie, edycja i usuwanie zakresów tożsamości (np. `openid`, `profile`, `email`, role i custom claims).
- 🛡️ **Zakresy API (ApiScopes):** Zarządzanie uprawnieniami i zakresami dostępu dla API.
- 👥 **Konta Użytkowników (Users):** Integracja z `UserManager` i `RoleManager` ASP.NET Core Identity.
- 🎫 **Aktywne Zgody i Tokeny (Persisted Grants):** Podgląd i natychmiastowe unieważnianie (Revoke) aktywnych Refresh Tokens i kodów OIDC.

## 🚀 Szybki Start

### 1. Rejestracja w `Program.cs`:
```csharp
using Quorum.Backend.AdminUI.Extensions;

// Rejestracja usług Admin UI z Twoim typem użytkownika (np. ApplicationUser lub IdentityUser)
builder.Services.AddQuorumAdminUI<ApplicationUser>(options =>
{
    options.RequiredRole = "Admin";
    options.EnableAuthorization = true;
});
```

### 2. Konfiguracja w potoku middleware:
```csharp
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer();
app.UseAuthorization();

// Rejestracja Razor Pages dla Admin UI
app.MapRazorPages();
```

Panel dostępny jest pod adresem: `/Admin`
