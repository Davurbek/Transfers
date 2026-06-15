# Dashboard Database Schema

The Dashboard DB holds (a) a read-optimized replica of transaction data and
(b) the auth + audit tables. It is **separate** from the Main App DB.

## Auth tables (Hybrid RBAC + PBAC)

```
users                      roles                     permissions
─────────────              ─────────────             ───────────────
id (PK)                    id (PK)                   id (PK)
username (UQ)              name (UQ)                 code (UQ)   e.g. tx:unpause
email                      description               description
password_hash
is_active
created_at

user_roles (M:N)           role_permissions (M:N)    user_permissions (M:N)
─────────────              ────────────────────      ──────────────────────
user_id  (FK users)        role_id (FK roles)        user_id (FK users)
role_id  (FK roles)        permission_id (FK perm)   permission_id (FK perm)
PK(user_id, role_id)       PK(role_id, permission_id) PK(user_id, permission_id)

refresh_tokens
──────────────
id (PK)
user_id (FK users)
token_hash (UQ, indexed)   -- store hash, never the raw token
expires_at
revoked_at (nullable)      -- instant revocability / session kill
created_at
created_by_ip
```

**Effective permissions** for a user =
`user_permissions` ∪ (`user_roles` → `role_permissions`).

## Transaction replica tables (read-optimized)

```
transactions
────────────
id (PK)
transaction_id (UQ)                -- business id, INDEXED
user_id                            -- remitter, INDEXED
recipient_name
amount
currency
corridor                           -- e.g. "UZ->RU"
current_status                     -- enum (see below)
is_paused
created_at                         -- INDEXED
updated_at
INDEX (user_id, created_at)
INDEX (current_status)

transaction_status_history
──────────────────────────
id (PK)
transaction_id (FK, INDEXED)
from_status (nullable)
to_status
reason
occurred_at (INDEXED)
event_id (UQ)                      -- idempotency key

credit_attempts                    -- crediting funds to local recipient
──────────────
id (PK)
transaction_id (FK, INDEXED)
attempt_number
gateway                            -- Humo | Uzcard
status                            -- Succeeded | Failed
failure_code (nullable)
gateway_response                   -- raw response payload
attempted_at (INDEXED)

partner_registrations              -- downstream partner network registration
─────────────────────
id (PK)
transaction_id (FK, INDEXED)
partner_name
status                            -- Succeeded | Failed
failure_reason (nullable)
reference_id (nullable)
registered_at (INDEXED)
```

### Transaction status enum

```
ConfirmPending
ConfirmSucceeded
ConfirmFailed
CreditPending
CreditSucceeded
CreditFailed
RegistrationPending
RegistrationFailedRetry
RegistrationSucceeded
Paused
Cancelled
```

## Audit table (immutable)

```
audit_logs
──────────
id (PK)
user_id (FK users)
action_type                        -- e.g. "tx:unpause"
target_transaction_id
timestamp (INDEXED)
ip_address
metadata (nullable, JSON)
```

Audit rows are append-only; the API never updates or deletes them.

## Indexing strategy

Per the TRD, reads/searches are optimized via indexes on `transaction_id`,
`user_id`, and `timestamp`-like columns (`created_at`, `occurred_at`,
`attempted_at`, `registered_at`), plus a composite `(user_id, created_at)` index
for the common "transactions for a user, newest first" query.
