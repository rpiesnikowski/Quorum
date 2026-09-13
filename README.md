# Quorum Identity & Access Platform

> ⚠️ **~90% AI-Assisted Development Project**  
> Source code, module architecture, and protocol integrations were developed predominantly with the assistance of modern AI models under engineering supervision.

---

## 📌 About the Project

**Quorum** is an experimental, modern identity and access control platform that unites standard OIDC/OAuth2 specifications with a decentralized decision-making model inspired by consensus mechanisms found in blockchain technologies.

The central pillar of the system is **Quorum-Based Authorization & Authentication**:
- Critical identity operations (such as assigning privileged roles, privilege escalation, or approving long-lived/elevated grants) cannot be executed unilaterally by a single administrator.
- A defined consensus threshold is required (**a quorum of authorized approvers/nodes**), analogous to multi-signature (multi-sig) transactions and governance voting mechanisms in distributed ledgers.

---

## 🏗️ Architecture & Core Components

The project comprises a cohesive ecosystem of modules built on **.NET 10**:

```
                  ┌────────────────────────┐
                  │     Client / Browser   │
                  └───────────┬────────────┘
                              │
                              ▼
                  ┌────────────────────────┐
                  │ Quorum.Backend.Gateway │ ◄── [OIDC Scopes + AuthZEN PEP]
                  └─────┬────────────┬─────┘
                        │            │
         ┌──────────────┘            └──────────────┐
         ▼                                          ▼
┌─────────────────────────┐              ┌───────────────────────────┐
│ Quorum.Backend          │              │ Quorum.Backend.AdminAPI   │
│ (OIDC / Identity Server)│              │  - PDP & PIP (AuthZEN)    │
└─────────────────────────┘              │  - Quorum Governance      │
         ▲                               └─────────────▲─────────────┘
         │                                             │
         └──────────────────────┬──────────────────────┘
                                │
                  ┌─────────────┴──────────┐
                  │ Quorum.Backend.AdminUI │
                  │ (Full Blazor GUI)      │
                  └────────────────────────┘
```

### 1. Identity Server (`Quorum.Backend`)
The OIDC/OAuth2 foundation based on the widely-adopted Open Identity Server ecosystem. It provides:
- Issuance, renewal, and revocation of security tokens (Access Tokens, ID Tokens, Refresh Tokens),
- Support for standard flows (Authorization Code + PKCE, Client Credentials),
- Centralized registry for users, roles, and identity resources.

### 2. Administrative GUI (`Quorum.Backend.AdminUI` & `AdminAPI`)
A full-featured administrative interface (Blazor / Radzen Components) dedicated to managing the identity ecosystem:
- **OIDC Configuration**: Clients, API Scopes, and Identity Resources,
- **Users & Permissions**: User registration, role assignments, identity federations,
- **Grant & Quorum Management**: Oversight and multi-party approval workflows for sensitive grants,
- **AuthZEN Policy Editor & Simulator**: Graphical policy builder and an interactive simulation tool for authorization requests.

### 3. Intelligent Gateway (`Quorum.Backend.Gateway`)
A high-performance reverse proxy and enforcement node:
- **Traffic Routing & Inspection**: Transparent request proxying to downstream backend services,
- **Scope Enforcement (OIDC Scopes)**: Route-level token validation and privilege checks,
- **AuthZEN Standard Implementation (OpenID Foundation)**:
  - Functions as a **PEP (Policy Enforcement Point)**,
  - Dispatches standardized evaluation queries to the **PDP (Policy Decision Point)** conforming to the [AuthZEN Working Group](https://openid.net/wg/authzen/) specification,
  - Enables dynamic, fine-grained Attribute-Based and Policy-Based Access Control (ABAC/PBAC) enriched with PIP (Policy Information Point) context.

---

## 🗺️ Roadmap & Future Horizons

### 🛡️ Zero Trust Policy Enforcement
Transitioning the platform toward a comprehensive **Zero Trust Architecture** ("Never Trust, Always Verify"):
- Continuous identity and device posture evaluation instead of perimeter-based trust,
- Contextual adaptive authorization that factors in device health, network origin, time, and behavioral signals.

### 🖥️ Native Endpoint Client (Windows & Cross-Platform Service)
Subsequent development phases include developing a native OS client running as a background system service (with Windows as the primary target, followed by Linux and macOS):
- **Local Reverse Proxy & HTTPS Decryption**:
  - Intercepts and decrypts TLS/HTTPS traffic locally at the operating system level,
  - Transparently identifies access requirements for protected targets across both **On-Premises** infrastructure and cloud environments (such as **Microsoft Azure**),
  - Automatically delegates and requests required permissions or triggers multi-party quorum approval flows when an unauthorized or elevated resource is accessed.
- **System-Wide Malware & Threat Filter**:
  - Operates as a persistent background service mediating traffic originating from web browsers as well as any arbitrary desktop application,
  - Deep packet and payload inspection to detect and block malicious content, exploits, and untrusted payloads in real time before reaching client processes.

---

## ⚡ Tech Stack

- **Runtime & Language**: .NET 10 / C# 13
- **Frontend / UI**: Blazor Web (Radzen Blazor Components), Vite / React UI
- **Routing & Proxy**: YARP / ASP.NET Core Middleware
- **Database & ORM**: Entity Framework Core, SQLite / SQL Server / PostgreSQL
- **Orkiestracja / Dev Environment**: .NET Aspire (`Quorum.AppHost`, `Quorum.ServiceDefaults`)
- **Authorization Standards**: OIDC / OAuth 2.0 + **AuthZEN Draft Specification**

---

## 🚀 Quick Start (Local Development)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)

```bash
# Clone the repository
git clone https://github.com/your-org/quorum.git
cd quorum

# Restore dependencies and build the solution
dotnet restore Quorum.slnx
dotnet build Quorum.slnx

# Launch the entire solution via .NET Aspire
dotnet run --project Quorum.AppHost/Quorum.AppHost.csproj
```

---

## 📄 License

Distributed under an open-source license (MIT / Apache 2.0). See `LICENSE` for details.
