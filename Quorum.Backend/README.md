# Quorum — Serwer Tożsamości .NET 10 (Open.IdentityServer)

Kompletne rozwiązanie (nowoczesna solucja `Quorum.slnx` wraz z projektami `Quorum.Backend` i biblioteką `Quorum.Backend.AdminUI`) serwera tożsamości **OpenID Connect** i **OAuth 2.0** oparte na:
* **.NET 10.0 (C# 13)**
* **Nowoczesny format solucji XML: `Quorum.slnx`**
* **Open.IdentityServer 2.0.0** (od Rock Solid Knowledge)
* **ASP.NET Core Identity** (lokalne konta użytkowników i role)
* **Entity Framework Core** z automatyczną obsługą **SQLite** oraz **PostgreSQL**
* **Dedykowany pakiet / biblioteka Razor Class Library `Quorum.Backend.AdminUI`** publikowalna jako paczka NuGet z kompletnym panelem administracyjnym CRUD

---

## 📦 Struktura Solucji (`Quorum.slnx`)

1. **`Quorum.Backend`** – Główny host ASP.NET Core z silnikiem Open.IdentityServer, endpointami OIDC, autentykacją ASP.NET Core Identity i obsługą EF Core (SQLite / PostgreSQL).
2. **`Quorum.Backend.AdminUI`** – Niezależna biblioteka **Razor Class Library (RCL)** przygotowana pod dystrybucję jako pakiet **NuGet** (`GeneratePackageOnBuild = true`). Udostępnia metody rozszerzeń `AddQuorumAdminUI<TUser>()` i `UseQuorumAdminUI()`.

---

## 🚀 Szybkie Uruchomienie

### 1. Uruchomienie z bazą SQLite (domyślnie)

Zaufaj deweloperskiemu certyfikatowi HTTPS (jednorazowo w systemie):
```bash
dotnet dev-certs https --trust
```

Uruchom aplikację (.NET 10):
```bash
# Opcja 1: Uruchomienie z poziomu solucji (.slnx)
dotnet restore Quorum.slnx
dotnet run --project Quorum.Backend

# Opcja 2: Zbudowanie pakietu NuGet z Admin UI
dotnet pack Quorum.Backend.AdminUI/Quorum.Backend.AdminUI.csproj -c Release -o ./artifacts
```

Aplikacja wystawia punkty końcowe:
* **HTTPS:** [https://localhost:5001](https://localhost:5001)
* **HTTP:** [http://localhost:5000](http://localhost:5000)
* **Panel Administratora:** [https://localhost:5001/Admin](https://localhost:5001/Admin)
* **Metadane OpenID Discovery:** [https://localhost:5001/.well-known/openid-configuration](https://localhost:5001/.well-known/openid-configuration)
* **Domyślny login admina:** `admin`
* **Domyślne hasło:** `Pass123$`

---

## 🗄️ Przełączenie na PostgreSQL

W pliku `appsettings.json` zmień wartość `DatabaseProvider`:

```json
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=identity_db;Username=postgres;Password=mojehaslo;"
  }
}
```

Aplikacja przy starcie automatycznie utworzy wszystkie schematy i tabele w PostgreSQL!

---

## 📋 Zawartość Panelu CRUD (Razor Pages)

1. **/Admin/Clients** – Dodawanie, edycja, usuwanie i konfiguracja klientów OAuth2 (Authorization Code, PKCE, Client Credentials, Redirect URIs).
2. **/Admin/ApiScopes** – Zarządzanie zakresami API (Scopes) i ich uprawnieniami.
3. **/Admin/IdentityResources** – Konfiguracja zasobów OIDC (`openid`, `profile`, `email`).
4. **/Admin/Users** – Zarządzanie lokalnymi kontami `AspNetIdentity` (tworzenie użytkowników, resetowanie haseł, przypisywanie ról `Admin`, `Manager`, `User`).
5. **/Admin/Grants** – Monitorowanie aktywnych tokenów odświeżających (Refresh Tokens) oraz unieważnianie zgód (Revocation).
