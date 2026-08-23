# Quorum — Serwer Tożsamości .NET 10 (Open.IdentityServer)

Kompletne rozwiązanie (nowoczesna solucja `Quorum.slnx` wraz z projektem `Quorum.Backend`) serwera tożsamości **OpenID Connect** i **OAuth 2.0** oparte na:
* **.NET 10.0 (C# 13)**
* **Nowoczesny format solucji XML: `Quorum.slnx`**
* **Open.IdentityServer 2.0.0** (od Rock Solid Knowledge)
* **ASP.NET Core Identity** (lokalne konta użytkowników i role)
* **Entity Framework Core** z automatyczną obsługą **SQLite** oraz **PostgreSQL**
* **Serwerowe GUI Administracyjne** w **Razor Pages** stylizowane biblioteką **Bootstrap 5**

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

# Opcja 2: Uruchomienie z profilem HTTPS z launchSettings.json
cd Quorum.Backend
dotnet run --launch-profile https
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
