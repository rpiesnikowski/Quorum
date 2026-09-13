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
   - **Operations**: `check`, `write`, `read`, `expand`.
   - **AuthZEN-to-OpenFGA Adapter**: Bi-directional bridge translating incoming AuthZEN evaluation requests directly into OpenFGA relationship checks.

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
│   │   └── AuthZenToOpenFgaAdapter.cs         # AuthZEN <-> OpenFGA translation
│   ├── Models/
│   │   └── OpenFgaModels.cs                   # TupleKey, CheckRequest, CheckResponse
│   └── Services/
│       ├── IOpenFgaClient.cs
│       └── OpenFgaClient.cs                   # In-memory graph + HTTP client
├── Extensions/
│   └── FineGrainedAuthServiceCollectionExtensions.cs # DI bootstrap methods
└── UI/                                        # Blazor & React simulator components
```

---

## 🚀 Getting Started

### 1. Register in Dependency Injection

```csharp
using Quorum.FineGrainedAuth.Extensions;

// Register AuthZEN PDP, PIP, and PAP
builder.Services.AddAuthZenPdp<ApplicationUser>();

// Register OpenFGA Client & Adapter
builder.Services.AddOpenFga();

// Or register full fine-grained auth suite:
builder.Services.AddFineGrainedAuth<ApplicationUser>();
```

### 2. Check Authorization via AuthZEN PEP

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
