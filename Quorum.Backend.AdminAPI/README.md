# Quorum.Backend.AdminAPI

Kompletny pakiet **REST API** dla Open.IdentityServer, ASP.NET Core Identity, OIDC Federations i API Gateway.

## Architektura i Przeznaczenie

Pakiet `Quorum.Backend.AdminAPI` oddziela warstwę interfejsu graficznego (np. Blazor / Radzen w `Quorum.Backend.AdminUI`, aplikacje React, Angular, Vue, MAUI, aplikacje mobilne czy skrypty automatyzacji CI/CD) od warstwy bazy danych i silnika tożsamości.

Dzięki temu panel administracyjny lub systemy zewnętrzne komunikują się poprzez spójne, w 100% zgodne ze standardem REST i OpenAPI endpointy HTTP.

---

## Moduły REST API

1. **Dashboard (`/api/admin/dashboard`)**
   - `GET /api/admin/dashboard/stats` - Zagregowane statystyki klientów, użytkowników, tras gateway, aktywnych grantów.

2. **Klienci OAuth2 / OIDC (`/api/admin/clients`)**
   - `GET /api/admin/clients` - Stronicowana lista klientów z wyszukiwaniem.
   - `GET /api/admin/clients/{id}` - Szczegóły klienta.
   - `POST /api/admin/clients` - Rejestracja nowego klienta.
   - `PUT /api/admin/clients/{id}` - Aktualizacja klienta.
   - `DELETE /api/admin/clients/{id}` - Usunięcie klienta.
   - `POST /api/admin/clients/{id}/secrets` - Dodanie sekretu klienta.
   - `DELETE /api/admin/clients/{id}/secrets/{secretId}` - Usunięcie wskazanego sekretu.

3. **Zakresy API / Scopes (`/api/admin/scopes`)**
   - `GET /api/admin/scopes` - Lista zakresów API.
   - `GET /api/admin/scopes/{id}` - Szczegóły zakresu.
   - `POST /api/admin/scopes` - Utworzenie nowego zakresu.
   - `PUT /api/admin/scopes/{id}` - Aktualizacja zakresu.
   - `DELETE /api/admin/scopes/{id}` - Usunięcie zakresu.

4. **Zasoby Tożsamości / Identity Resources (`/api/admin/identity-resources`)**
   - `GET /api/admin/identity-resources` - Lista zasobów tożsamości.
   - `GET /api/admin/identity-resources/{id}` - Szczegóły zasobu.
   - `POST /api/admin/identity-resources` - Utworzenie nowego zasobu.
   - `PUT /api/admin/identity-resources/{id}` - Aktualizacja zasobu.
   - `DELETE /api/admin/identity-resources/{id}` - Usunięcie zasobu.
   - `POST /api/admin/identity-resources/seed` - Inicjalizacja standardowych zasobów OIDC (`openid`, `profile`, `email`, `address`, `phone`).

5. **Użytkownicy / ASP.NET Identity (`/api/admin/users`)**
   - `GET /api/admin/users` - Lista użytkowników.
   - `GET /api/admin/users/{id}` - Szczegóły użytkownika.
   - `POST /api/admin/users` - Utworzenie konta użytkownika z hasłem i rolami.
   - `PUT /api/admin/users/{id}` - Aktualizacja profilu i ról.
   - `DELETE /api/admin/users/{id}` - Usunięcie konta.
   - `POST /api/admin/users/{id}/change-password` - Resetowanie/zmiana hasła.
   - `POST /api/admin/users/{id}/toggle-lockout` - Blokowanie i odblokowywanie konta.
   - `GET /api/admin/users/roles` - Lista dostępnych ról systemowych.

6. **Federacje OIDC / SSO (`/api/admin/federations`)**
   - `GET /api/admin/federations` - Lista zewnętrznych IdP (Google, Azure AD, Keycloak).
   - `GET /api/admin/federations/{id}` - Szczegóły dostawcy.
   - `POST /api/admin/federations` - Rejestracja nowego dostawcy OIDC.
   - `PUT /api/admin/federations/{id}` - Aktualizacja konfiguracji.
   - `DELETE /api/admin/federations/{id}` - Usunięcie dostawcy.
   - `POST /api/admin/federations/{id}/toggle` - Włączenie/wyłączenie dostawcy.
   - `POST /api/admin/federations/test-discovery` - Walidacja i test endpointu `.well-known/openid-configuration`.

7. **API Gateway & Reverse Proxy (`/api/admin/gateway`)**
   - `GET /api/admin/gateway/routes` - Lista skonfigurowanych tras proxy.
   - `GET /api/admin/gateway/routes/{id}` - Szczegóły trasy.
   - `POST /api/admin/gateway/routes` - Dodanie nowej trasy (szablony `{grupa}`, Regex, adres upstream, nagłówki).
   - `PUT /api/admin/gateway/routes/{id}` - Aktualizacja trasy.
   - `DELETE /api/admin/gateway/routes/{id}` - Usunięcie trasy.
   - `POST /api/admin/gateway/test` - Tester/symulator dopasowania tras z ekstrakcją grup parametrów.

8. **Aktywne Granty i Tokeny (`/api/admin/grants`)**
   - `GET /api/admin/grants` - Lista aktywnych tokenów i zgód.
   - `GET /api/admin/grants/{key}` - Szczegóły pojedynczego grantu.
   - `DELETE /api/admin/grants/{key}` - Unieważnienie grantu.
   - `DELETE /api/admin/grants/subject/{subjectId}` - Unieważnienie wszystkich sesji użytkownika.
   - `DELETE /api/admin/grants/client/{clientId}` - Unieważnienie wszystkich tokenów klienta.

---

## Rejestracja w Program.cs

```csharp
// 1. Rejestracja usług REST API
builder.Services.AddQuorumAdminApi(options =>
{
    options.RoutePrefix = "api/admin";
    options.RequiredRole = "Admin";
});

// 2. Mapowanie endpointów w potoku HTTP
app.MapQuorumAdminApi();
```
