# Quorum.FineGrainedAuth - Fine-Grained Authorization Engine (AuthZEN & OpenFGA)

> **Decoupled Fine-Grained Authorization Subsystem**  
> Implementing the **OpenID Foundation AuthZEN 1.0** specification and **OpenFGA (Google Zanzibar model)** for Attribute-Based (ABAC) and Relationship-Based (ReBAC) Access Control.

---

## 🎯 Architectural Overview

`Quorum.FineGrainedAuth` is an independent, high-performance authorization service and class library designed to decouple access decision logic from core application and identity components.

### Supported Standards & Frameworks:
1. **AuthZEN 1.0 (OpenID Foundation RFC)**
   - **PDP (Policy Decision Point)**: High-throughput decision engine evaluating Subject, Action, Resource, and Context.
   - **PIP (Policy Information Point)**: Attribute resolution layer integrating identity providers, roles, user status, and claims.
   - **PAP (Policy Administration Point)**: Dynamic management of policy rules with priority ordering and condition expression evaluation.
   - **PEP (Policy Enforcement Point)**: Resilient fail-closed HTTP/middleware client for API gateways and microservices.
   - **Standard REST Endpoints**: `POST /access/v1/evaluation` and `POST /access/v1/evaluations`.

2. **OpenFGA (Fine-Grained Authorization / Google Zanzibar)**
   - **Relationship-Based Access Control (ReBAC)**: Fine-grained permissions represented as tuples `(user) is [relation] of (object)`.
   - **Tuple Graph Engine**: Direct relationships, transitive inheritance (`owner`, `admin`), contextual tuples, and wildcard resolution.
   - **REST API Integration**: Direct communication with local OpenFGA instances (`http://0.0.0.0:8080` / `http://localhost:8080`) supporting `POST /stores/{id}/write`, `POST /stores/{id}/read`, `POST /stores/{id}/check`, and store auto-provisioning.
   - **AuthZEN-to-OpenFGA Adapter & CRUD**: Create and edit authorization rules using AuthZEN syntax (Subject, Action, Resource, Effect), while the adapter automatically transforms them into Zanzibar tuples and writes them directly to the OpenFGA REST API.

---

## 📁 Project Structure

```
Quorum.FineGrainedAuth/
├── AuthZen/
│   ├── Controllers/
│   │   ├── AdminAuthZenPoliciesController.cs  # PAP REST CRUD API
│   │   └── AuthZenEvaluationController.cs     # PDP /access/v1/evaluation endpoint
│   ├── Data/
│   │   ├── AuthZenDbContext.cs                # EF Core context for AuthZEN
│   │   └── IAuthZenDbContext.cs
│   ├── Models/
│   │   ├── AuthZenModels.cs                   # Standard AuthZEN DTOs
│   │   ├── AuthZenPolicy.cs                   # Policy entity
│   │   └── AuthZenPolicyAdminModel.cs         # Administrative model
│   ├── Services/
│   │   ├── PDP/                               # Policy Decision Point engine
│   │   ├── PIP/                               # Identity attribute enrichment
│   │   └── PEP/                               # Enforcement client
│   └── Stores/
│       ├── IAuthZenPolicyStore.cs
│       └── EfAuthZenPolicyStore.cs
├── OpenFGA/
│   ├── Adapters/
│   │   └── AuthZenToOpenFgaAdapter.cs         # AuthZEN <-> OpenFGA translation & REST sync
│   ├── Controllers/
│   │   └── OpenFgaRulesController.cs          # Full CRUD API for rules & OpenFGA REST sync
│   ├── Models/
│   │   ├── OpenFgaModels.cs                   # TupleKey, CheckRequest, DTOs, Store models
│   │   └── OpenFgaOptions.cs                  # ServerUrl, StoreId, AutoCreate configuration
│   └── Services/
│       ├── IOpenFgaClient.cs                  # Client interface for REST & In-Memory
│       ├── OpenFgaClient.cs                   # Direct HTTP REST client for 0.0.0.0:8080
│       ├── IAuthZenOpenFgaStore.cs            # CRUD store interface
│       └── AuthZenOpenFgaStore.cs             # Implementation with live adapter sync
├── Extensions/
│   └── FineGrainedAuthServiceCollectionExtensions.cs # DI bootstrap methods
└── UI/                                        # Blazor & React simulator components
    └── Components/
        ├── OpenFgaRuleManager.razor           # Interactive CRUD UI in Radzen Blazor
        └── OpenFgaRuleManager.razor.cs
```

---

## 🚀 Getting Started

### 1. Register in Dependency Injection and Blazor Routing

W pliku `Program.cs` projektu hosta (`Quorum.Backend`):

```csharp
using Quorum.FineGrainedAuth.Extensions;

// 1. Rejestracja serwisów AuthZEN PDP, PIP, PAP oraz OpenFGA:
builder.Services.AddFineGrainedAuth<ApplicationUser>(options =>
{
    options.OpenFga.ApiUrl = "http://localhost:8080"; // Adres Twojego lokalnego serwera OpenFGA
    // options.OpenFga.StoreId = "TWÓJ_STORE_ID";     // Opcjonalnie (jeśli pusty, pobierany/tworzony automatycznie)
});

// 2. Kluczowa rejestracja stron Blazor GUI z assembly Quorum.FineGrainedAuth:
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Quorum.Backend.AdminUI.Components.Layout.AdminLayout).Assembly,
        typeof(Quorum.FineGrainedAuth.UI.Components.AuthZenPoliciesList).Assembly // <- Wymagane, aby Blazor rozpoznał trasy GUI!
    );

// 3. Mapowanie kontrolerów REST API:
app.MapControllers();
```

### 2. Dostępne trasy GUI w panelu administracyjnym

Po rejestracji komponenty GUI są dostępne bezpośrednio pod trasami:
* **`/admin/authzen/policies`** – Główny widok CRUD polityk AuthZEN (Radzen DataGrid, filtry, akcje edycji i usuwania)
* **`/admin/authzen/policies/create`** – Formularz tworzenia nowej reguły
* **`/admin/authzen/policies/{id}/edit`** – Formularz edycji reguły
* **`/admin/openfga/rules`** – Menadżer reguł OpenFGA z automatyczną synchronizacją REST z serwerem OpenFGA (`http://0.0.0.0:8080`)
* **`/admin/authzen/simulator`** – Interaktywny symulator ewaluacji PDP + PIP w czasie rzeczywistym
* **`/api/admin/authzen/policies`** – REST API dla automatyzacji i zewnętrznych narzędzi

### 3. Check Authorization via AuthZEN PEP

```csharp
var pepRequest = new AuthZenEvaluationRequest
{
    Subject = new AuthZenSubject { Type = "user", Id = "john.doe" },
    Action = new AuthZenAction { Name = "GET" },
    Resource = new AuthZenResource { Type = "route", Id = "/api/finance/reports" }
};

var response = await pepClient.EvaluateAsync(pepRequest);
if (!response.Decision)
{
    // Access Denied
}
```

### 3. Check Authorization via OpenFGA

```csharp
var checkResult = await openFgaClient.CheckAsync(new FgaCheckRequest
{
    TupleKey = new FgaTupleKey
    {
        User = "user:anne",
        Relation = "viewer",
        Object = "document:quarterly_report"
    }
});
```

---

## 🔒 License
MIT License. Part of the Quorum Security Architecture.
