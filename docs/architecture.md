# Architecture & Data-Sync Design

## 1. Goals

- **Isolation:** Protect the transaction-processing engine from heavy read/search
  load by giving the dashboard its own DB, API, and UI.
- **Eventual consistency:** Replicate transaction lifecycle data asynchronously.
- **Safe writes:** Allow privileged write actions (e.g. *Unpause*) without the
  dashboard ever mutating the Main DB.

## 2. Components

| Component | Responsibility |
|---|---|
| **Main App** | Source of truth. Owns the transaction state machine. Emits lifecycle events. Consumes command messages. |
| **Message Broker** | Kafka or RabbitMQ. Two logical channels: `transfers.events.*` (Main → Dashboard) and `transfers.commands.*` (Dashboard → Main). |
| **Dashboard API (.NET 10)** | Consumes events into the Dashboard DB. Serves read APIs. Publishes command messages for write actions. Enforces AuthN/AuthZ, audit logging, rate limiting. |
| **Dashboard DB** | Read-optimized replica + auth + audit tables. |
| **Dashboard UI (Vue 3)** | Operator-facing SPA. Permission-aware rendering. |

## 3. Data-sync flow (read path)

```
Main App                Broker (events)                Dashboard API           Dashboard DB
   │                          │                              │                       │
   │  TxStatusChanged event   │                              │                       │
   ├─────────────────────────▶│                              │                       │
   │                          │   consume (IEventConsumer)   │                       │
   │                          ├─────────────────────────────▶│  upsert + append      │
   │                          │                              ├──────────────────────▶│
   │                          │                              │  history row          │
```

Event types consumed:

- `TransactionUpserted` — full snapshot of a transaction (idempotent upsert).
- `TransactionStatusChanged` — appends a `transaction_status_history` row and
  updates `transactions.current_status`.
- `CreditAttemptRecorded` — appends a `credit_attempts` row (Humo/Uzcard).
- `PartnerRegistrationRecorded` — appends a `partner_registrations` row.

All event handlers are **idempotent** (keyed on event id / natural keys) so
re-delivery is safe.

## 4. Write flow (command path) — e.g. *Unpause*

```
UI            Dashboard API                Broker (commands)        Main App
 │  POST /unpause   │                            │                     │
 ├─────────────────▶│  authorize tx:unpause      │                     │
 │                  │  write AuditLog (immutable) │                     │
 │                  │  publish UnpauseCommand     │                     │
 │                  ├────────────────────────────▶│  consume command    │
 │                  │                            ├────────────────────▶│  re-run state machine
 │   202 Accepted   │                            │                     │
 │◀─────────────────┤                            │   TxStatusChanged    │
 │                  │   consume event (read path)│◀────────────────────┤
 │  poll / refresh  │   update Dashboard DB view │                     │
```

- The dashboard returns **202 Accepted** — the action is *requested*, not *applied*.
- The new state appears only after the Main App processes the command and the
  resulting event is consumed back. This keeps the Main App as the single source
  of truth.

## 5. Broker abstraction in this PoC

`ICommandPublisher` and `IEventConsumer` abstract the broker. The PoC ships a
`SimulatedBroker` that:

1. Receives an `UnpauseCommand` from the API.
2. Mimics the Main App: flips the paused transaction to a retrying/succeeded
   state after a short delay.
3. Emits a `TransactionStatusChanged` event back into the consumer, which updates
   the Dashboard DB — demonstrating the full round-trip in real time.

In production, replace `SimulatedBroker` with a Kafka/RabbitMQ client; no API or
UI code changes are required.

## 6. Security overview

- **AuthN:** JWT access token (15 min, `Authorization: Bearer`) + refresh token
  (1–7 days) stored server-side for revocability, delivered to the browser via an
  `HttpOnly; Secure; SameSite=Strict` cookie.
- **AuthZ:** Hybrid RBAC + PBAC. Effective permissions = direct user permissions
  ∪ permissions from all assigned roles. Endpoints check **permission strings**
  (`tx:unpause`), never role names.
- **Audit:** Every write action stores `user_id`, `action_type`,
  `target_transaction_id`, `timestamp`, `ip_address`.
- **Rate limiting:** Fixed/sliding-window limiters on `auth` and `mutation`
  endpoints.
