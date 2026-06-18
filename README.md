# Transfers — Operational Dashboard

A decoupled, **read-mostly Operational Dashboard** for the cross-border remittance
("Transfers") service. It runs on its own UI, its own backend API, and its own
isolated database so that heavy analytical / search queries never touch the
transaction-processing engine.

This repository implements the **Proof-of-Concept** described in the project TRD:

- A **.NET 10** backend API (`backend/`)
- A **Vue 3 + Vite + TypeScript** frontend (`frontend/`)
- Architecture & data-model docs (`docs/`)

---

## What it does

| Capability | Type | Notes |
|---|---|---|
| Transaction list & search | Read | Indexed on `transaction_id`, `user_id`, `timestamp` |
| Lifecycle / status-transition history | Read | Full timestamped audit trail |
| Credit-attempt history (Humo / Uzcard) | Read | Gateway response & failure codes |
| Partner registration history | Read | Downstream partner network results |
| **Unpause transaction** | Write | Publishes a *command* to the Main App; never writes the main DB directly |
| Authentication | — | JWT access token + refresh token (HttpOnly cookie) |
| Authorization | — | Hybrid **RBAC + PBAC**, enforced by permission strings |
| Audit logging | — | Immutable record of every write action |
| Rate limiting | — | Strict limits on auth + mutation endpoints |

---

## Architecture at a glance

```
                 lifecycle events                    commands (e.g. tx:unpause)
 ┌───────────────┐  ───────────────▶  ┌──────────────┐  ◀───────────────  ┌─────────────────┐
 │  Main App     │                    │ Message      │                    │ Dashboard API   │
 │  (source of   │  ◀───────────────  │ Broker       │  ───────────────▶  │ (.NET 10)       │
 │   truth)      │  state-change ev.  │ (Kafka/RMQ)  │   consume events   │ + Dashboard DB  │
 └───────────────┘                    └──────────────┘                    └────────┬────────┘
                                                                                    │ JWT / REST
                                                                                    ▼
                                                                           ┌─────────────────┐
                                                                           │ Dashboard UI    │
                                                                           │ (Vue 3)         │
                                                                           └─────────────────┘
```

Key rules (from the TRD):

1. The Dashboard **never** queries the Main DB directly and **never** runs
   `INSERT`/`UPDATE` on the main transaction tables.
2. Data is replicated **asynchronously** via broker events consumed by the dashboard.
3. Write actions are performed by **publishing a command** to a protected queue; the
   Main App stays the single source of truth and broadcasts the resulting
   state-change event back, which the dashboard consumes to update its view.

In this PoC the message broker is **abstracted behind interfaces**
(`IEventConsumer`, `ICommandPublisher`). A lightweight in-process simulator
(`SimulatedBroker`) plays the role of Kafka/RabbitMQ so the end-to-end flow
(unpause → command → simulated Main App → state-change event → dashboard view
update) is fully demonstrable without external infrastructure. Swap the
implementation for a real broker client in production.

See [`docs/architecture.md`](docs/architecture.md) and
[`docs/database-schema.md`](docs/database-schema.md) for details.

---

## Running locally

### Backend (.NET 10)

```bash
cd backend/Transfers.Dashboard.Api
dotnet restore
dotnet run
```

- API: `https://localhost:5001` (or the port printed in the console)
- Swagger UI: `/swagger`
- On first run the database is created and **seeded** with demo users,
  roles/permissions, and sample transactions.

> **Note:** This PoC uses **EF Core with SQLite** for zero-setup local runs.
> Swap to PostgreSQL by changing the provider in `Program.cs` and the
> connection string in `appsettings.json`.

### Frontend (Vue 3)

```bash
cd frontend
npm install
npm run dev
```

- UI: `http://localhost:5173`
- The dev server proxies `/api` to the backend (see `vite.config.ts`).

### Demo accounts (seeded)

| Username | Password | Role | Key permissions |
|---|---|---|---|
| `support` | `Passw0rd!` | Support_Level_1 | `tx:read` |
| `ops` | `Passw0rd!` | Operations_Manager | `tx:read`, `tx:unpause` |
| `compliance` | `Passw0rd!` | Compliance_Officer | `tx:read`, `tx:unpause`, `tx:cancel`, `audit:read` |

---

## Project layout

```
.
├── backend/
│   ├── Transfers.Dashboard.sln
│   ├── Transfers.Dashboard.Domain/        # Entities, enums, shared models (no deps)
│   ├── Transfers.Dashboard.DataAccess/    # DAL: DbContext, repositories, UnitOfWork, seeding
│   ├── Transfers.Dashboard.Business/      # BLL: services, DTOs, auth/token, messaging, mapping
│   └── Transfers.Dashboard.Api/           # Controllers, auth middleware, Program.cs
├── frontend/                              # Vue 3 + Vite + TS
├── docs/
│   ├── architecture.md
│   └── database-schema.md
└── README.md
```

## Layered architecture

The backend follows a clean, one-directional dependency flow:

```
Controller (API)  ->  Service (BLL)  ->  Repository (DAL)  ->  DbContext / DB
   thin HTTP          business logic      data access only      EF Core (SQLite)
```

- **Controllers** only translate HTTP <-> service calls (no EF, no business rules).
- **Services** hold all business logic (auth, permission resolution, unpause flow,
  audit) and map entities to DTOs.
- **Repositories** are the only layer that touches `DbContext`; an `IUnitOfWork`
  commits changes.
- **Domain** has zero dependencies and is shared by every layer.

Each layer exposes an `AddDataAccess(...)` / `AddBusiness(...)` DI extension so
`Program.cs` wiring stays small.

## Key API endpoints

| Method | Route | Permission | Notes |
|---|---|---|---|
| `POST` | `/api/auth/login` | — | Sets refresh cookie |
| `POST` | `/api/auth/refresh` | — | Rotates refresh token |
| `POST` | `/api/auth/logout` | — | Revokes refresh token |
| `GET` | `/api/auth/me` | (auth) | Current user + permissions |
| `GET` | `/api/transactions` | `tx:read` | **Filter + pagination** (see below) |
| `GET` | `/api/transactions/{id}` | `tx:read` | Lifecycle / credit / partner history |
| `POST` | `/api/transactions/{id}/unpause` | `tx:unpause` | Publishes command, writes audit, `202` |
| `GET` | `/api/audit` | `audit:read` | **Filter + pagination** audit log |

**Transactions filter query params:** `search`, `status`, `userId`, `isPaused`,
`fromDate`, `toDate`, `page` (default 1), `pageSize` (default 20, max 100).

**Audit filter query params:** `targetTransactionId`, `actionType`, `username`,
`fromDate`, `toDate`, `page` (default 1), `pageSize` (default 50, max 200).

Both return a `PagedResult<T>`:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 137,
  "totalPages": 7,
  "hasPrevious": false,
  "hasNext": true
}
```
