# Transfers Operational Dashboard — Toʻliq Hujjat

> **Maqsad:** Ushbu hujjatni oʻqigan dasturchi loyiha arxitekturasi, kodi va
> biznes mantiqini toʻliq tushunib, mustaqil davom ettira oladi.

---

## 1. Loyiha haqida

**Transfers Operational Dashboard** — xalqaro pul oʻtkazmalari (remittance)
tizimi uchun "read-mostly" operatsion panel. Asosiy vazifasi:

- Tranzaksiyalarni qidirish va koʻrish (read)
- Tranzaksiya hayotiy sikli (status history) ni kuzatish
- Credit attempt (Humo/Uzcard) va partner registration tarixini koʻrish
- **Paused** tranzaksiyalarni **Unpause** qilish (write)
- Audit log (barcha write amallari immutable tarzda yoziladi)
- Foydalanuvchilar, rollar va permissionlarni boshqarish (Admin CRUD)

**Prinsiplar (TRD boʻyicha):**
1. Dashboard **hech qachon** Main App DB'siga toʻgʻridan-toʻgʻri soʻrov yubormaydi
2. Ma'lumotlar **async** replica (broker eventlari orqali) keladi
3. Write amallari **command** publikatsiyasi orqali amalga oshadi

---

## 2. Arxitektura (Clean Architecture)

Loyiha 4 qatlamli (layered) Clean Architecture asosida qurilgan:

```
Api (Controller)
  │  HTTP, JWT, CORS, RateLimiting, Swagger
  │  references → Application
  ▼
Application (Service / BLL)
  │  Business logic, DTOs, mapping, auth services
  │  references → Domain
  ▼
Infrastructure (DAL / Data Access)
  │  EF Core DbContext, Repositories, Messaging, Seeding
  │  references → Domain
  ▼
Domain (Entities / Core)
  │  POCO entities, enums, interfaces, permissions
  │  hech narsaga reference qilmaydi
```

### 2.1 Qatlamlar tafsiloti

#### Domain (`backend/src/Domain/`)
| Papka | Mazmuni |
|---|---|
| `Auth/Entities/` | User, Role, Permission, UserRole, RolePermission, UserPermission, RefreshToken |
| `Auth/Interfaces/` | IAdminRepository, IPermissionRepository |
| `Auth/Exceptions/` | Maxsus exception turlari |
| `Auth/Permissions.cs` | Permission string constantalari (`TxRead = "tx:read"`, `TxUnpause = "tx:unpause"`, ...) |
| `Auth/Events/` | Domain eventlar |
| `Transactions/Entities/` | Transaction, TransactionStatusHistory, CreditAttempt, PartnerRegistration |
| `Transactions/Enums/` | TransactionStatus, CreditGateway, OperationResult |
| `Audit/Entities/` | AuditLog |
| `Common/Security/` | PasswordHasher |

#### Application (`backend/src/Application/`)
| Papka | Mazmuni |
|---|---|
| `Auth/` | AuthService (login/refresh/logout), PermissionService, DTOs, interfaces |
| `Admin/` | AdminService (User/Role/Permission CRUD), DTOs, IAdminService |
| `Transactions/` | TransactionService (search, detail, unpause), DTOs |
| `Audit/` | AuditService, DTOs |
| `Messaging/` | TransferEvent recordlari, ICommandPublisher, IEventConsumer interfeyslari |

#### Infrastructure (`backend/src/Infrastructure/`)
| Papka | Mazmuni |
|---|---|
| `Common/Persistence/` | AppDbContext (EF Core), konfiguratsiyalar |
| `Auth/Persistence/` | AdminRepository, PermissionRepository (implementatsiyalar) |
| `Transactions/Persistence/` | TransactionRepository |
| `Audit/Persistence/` | AuditRepository |
| `Messaging/` | SimulatedBroker, KafkaCommandPublisher, MassTransit |
| `Seeding/` | DbSeeder (demo ma'lumotlar) |

#### Api (`backend/src/Api/`)
| Papka | Mazmuni |
|---|---|
| `Auth/` | AuthController, JwtOptions, PermissionPolicyProvider, PermissionHandler |
| `Admin/` | AdminController (users/roles/permissions CRUD) |
| `Transactions/` | TransactionsController (search/detail/unpause) |
| `Audit/` | AuditController |
| `Common/Filters/` | ValidationFilter (ModelState validation) |
| `Common/Middlewares/` | ExceptionHandlingMiddleware (global exception handler) |
| `Messaging/` | DI registration for messaging |
| `Properties/` | launchSettings.json |

### 2.2 Frontend arxitekturasi

```
View / Component (presentation)
  │  Vue SFC, router, conditional rendering
  │  calls → Service
  ▼
Service (BLL)
  │  Business logic, caching, mapping
  │  calls → Repository (interface)
  ▼
Repository (DAL)
  │  HTTP calls (axios), data shaping
  │  calls → httpClient
  ▼
httpClient (Infrastructure)
  │  Axios instance, auth interceptor (401 → refresh → retry)
```

| Papka | Mazmuni |
|---|---|
| `domain/` | TypeScript turlari, permission constantalari (hech narsaga bogʻliq emas) |
| `infrastructure/` | httpClient.ts — yagona axios instance |
| `repositories/` | AdminRepository, AuthRepository, TransactionRepository, AuditRepository (+ interfaces) |
| `services/` | AdminService, AuthService, TransactionService, AuditService (+ interfaces) |
| `di/` | Composition root — repository → service bogʻlash |
| `stores/` | Pinia auth store (session state) |
| `composables/` | useTransactions, useAudit (presentation logic) |
| `components/` | AppHeader (sidebar), StatusBadge, PaginationBar |
| `views/` | Har bir sahifa uchun Vue SFC |

---

## 3. Data model (Entity Relationship)

### 3.1 Auth jadvallari

```
users                        roles                        permissions
┌──────────────────┐        ┌─────────────────┐          ┌────────────────────┐
│ id (PK, Guid)    │        │ id (PK, Guid)   │          │ id (PK, Guid)      │
│ username (UQ)    │        │ name (UQ)       │          │ code (UQ)          │
│ email            │        │ description     │          │ description        │
│ password_hash    │        └────────┬────────┘          └─────────┬──────────┘
│ is_active        │                 │                            │
│ created_at       │                 │                            │
└────────┬─────────┘                 │                            │
         │                          │                            │
         │   user_roles             │   role_permissions          │   user_permissions
         │   ┌──────────────┐       │   ┌──────────────────┐     │   ┌──────────────────┐
         ├──▶│ user_id (FK) │       ├──▶│ role_id (FK)     │     ├──▶│ user_id (FK)     │
         │   │ role_id (FK) │       │   │ permission_id(FK)│     │   │ permission_id(FK)│
         │   └──────────────┘       │   └──────────────────┘     │   └──────────────────┘
         │                          │                            │
         │   refresh_tokens         │                            │
         │   ┌──────────────────┐   │                            │
         └──▶│ id (PK)          │   │                            │
             │ user_id (FK)     │   │                            │
             │ token_hash (UQ)  │   │                            │
             │ expires_at       │   │                            │
             │ revoked_at (?)   │   │                            │
             │ created_at       │   │                            │
             │ created_by_ip    │   │                            │
             └──────────────────┘   │                            │
```

**Effective permissions** = `user_permissions` ∪ `role_permissions` (userning rollari orqali)

### 3.2 Transaction replica jadvallari

```
transactions
┌──────────────────────────┐
│ id (PK, Guid)            │
│ internal_ref             │
│ transaction_id (UQ)      │
│ user_id                  │
│ recipient_name           │
│ amount                   │
│ currency                 │
│ corridor (e.g. RU->UZ)   │
│ current_status           │
│ is_paused                │
│ created_at               │
│ updated_at               │
└────────────┬─────────────┘
             │
             │  transaction_status_history
             │  ┌──────────────────────────────┐
             ├──│ transaction_id (FK)          │
             │  │ from_status (nullable)       │
             │  │ to_status                    │
             │  │ reason                       │
             │  │ occurred_at                  │
             │  │ event_id (UQ - idempotency)  │
             │  └──────────────────────────────┘
             │
             │  credit_attempts
             │  ┌──────────────────────────────┐
             ├──│ transaction_id (FK)          │
             │  │ attempt_number               │
             │  │ gateway (Humo|Uzcard)        │
             │  │ status (Succeeded|Failed)    │
             │  │ failure_code (nullable)      │
             │  │ gateway_response             │
             │  │ attempted_at                 │
             │  └──────────────────────────────┘
             │
             │  partner_registrations
             │  ┌──────────────────────────────┐
             └──│ transaction_id (FK)          │
                │ partner_name                 │
                │ status (Succeeded|Failed)    │
                │ failure_reason (nullable)    │
                │ reference_id (nullable)      │
                │ registered_at                │
                └──────────────────────────────┘
```

### 3.3 Audit jadvali

```
audit_logs
┌──────────────────────────┐
│ id (PK, Guid)            │
│ user_id (FK users)       │
│ action_type               │  -- e.g. "tx:unpause"
│ target_transaction_id    │
│ timestamp                │
│ ip_address               │
│ metadata (JSON, nullable)│
└──────────────────────────┘
```

Audit yozuvlari **append-only** (hech qachon update/delete boʻlmaydi).

### 3.4 TransactionStatus enum

```
ConfirmPending → ConfirmSucceeded → CreditSucceeded → RegistrationSucceeded
                    ↓                    ↓                    ↓
               ConfirmFailed      CreditFailed        RegistrationFailedRetry
                              ↘         ↓         ↙
                               CreditFailedRetry
                                    ↓
                                Paused
                              ↙        ↘
                    RegistrationFailedRetry   (unpaused)
                         ↓
                   RegistrationSucceeded
```

---

## 4. API Endpointlar

### 4.1 Auth

| Method | Route | Auth | Rate Limit | Description |
|---|---|---|---|---|
| POST | `/auth/login` | No | `auth` (10/min) | Kirish, JWT + refresh cookie |
| POST | `/auth/refresh` | Cookie | `auth` | Refresh tokenni aylantirish |
| POST | `/auth/logout` | Yes | `auth` | Refresh tokenni bekor qilish |
| GET | `/auth/me` | Yes | — | Joriy user + permissionlar |

**Login request:**
```json
{ "username": "ops", "password": "Passw0rd!" }
```
**Login response:**
```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2025-...",
  "user": { "id": "...", "username": "ops", "email": "...",
            "permissions": ["tx:read", "tx:unpause"],
            "roles": ["Operations_Manager"] }
}
```

### 4.2 Transactions

| Method | Route | Permission | Rate Limit | Description |
|---|---|---|---|---|
| GET | `/transactions` | `tx:read` | — | Search + pagination |
| GET | `/transactions/{id}` | `tx:read` | — | Full detail (history, credits, partners) |
| POST | `/transactions/{id}/unpause` | `tx:unpause` | `mutations` (20/min) | Unpause command → 202 Accepted |

**Search query params:** `search`, `status`, `userId`, `isPaused`, `fromDate`, `toDate`, `page`, `pageSize`

### 4.3 Admin (User/Role/Permission CRUD)

| Method | Route | Permission | Description |
|---|---|---|---|
| GET | `/admin/users` | `admin` | Barcha userlar |
| GET | `/admin/users/{id}` | `admin` | User detail (roles + direct permissions) |
| POST | `/admin/users` | `admin` | Create user (optional `roleIds`) |
| PUT | `/admin/users/{id}` | `admin` | Update user |
| DELETE | `/admin/users/{id}` | `admin` | Deactivate user (soft delete) |
| POST | `/admin/users/{id}/roles` | `admin` | Role biriktirish |
| DELETE | `/admin/users/{id}/roles/{roleId}` | `admin` | Role olib tashlash |
| **POST** | **`/admin/users/{id}/permissions`** | **`admin`** | **Direct permission biriktirish** |
| **DELETE** | **`/admin/users/{id}/permissions/{permId}`** | **`admin`** | **Direct permission olib tashlash** |
| GET | `/admin/roles` | `admin` | Barcha rollar |
| GET | `/admin/roles/{id}` | `admin` | Role detail (permissions bilan) |
| POST | `/admin/roles` | `admin` | Create role |
| PUT | `/admin/roles/{id}` | `admin` | Update role |
| DELETE | `/admin/roles/{id}` | `admin` | Delete role |
| POST | `/admin/roles/{id}/permissions` | `admin` | Permission biriktirish |
| DELETE | `/admin/roles/{id}/permissions/{permId}` | `admin` | Permission olib tashlash |
| GET | `/admin/permissions` | `admin` | Barcha permissionlar |
| GET | `/admin/permissions/{id}` | `admin` | Permission detail |
| POST | `/admin/permissions` | `admin` | Create permission |
| PUT | `/admin/permissions/{id}` | `admin` | Update permission |
| DELETE | `/admin/permissions/{id}` | `admin` | Delete permission |

### 4.4 Audit

| Method | Route | Permission | Description |
|---|---|---|---|
| GET | `/audit` | `audit:read` | Audit log search + pagination |

**Search query params:** `targetTransactionId`, `actionType`, `username`, `fromDate`, `toDate`, `page`, `pageSize`

### 4.5 Health

| Method | Route | Description |
|---|---|---|
| GET | `/health` | DB connection check |
| GET | `/health/ready` | Ready check |
| GET | `/health/live` | Alive check |

### 4.6 PagedResult formati

Barcha search endpointlari quyidagi formatda javob qaytaradi:

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 137,
  "totalPages": 7,
  "hasPrevious": false,
  "hasNext": true
}
```

---

## 5. Autentifikatsiya (AuthN)

### 5.1 Login flow

1. `POST /auth/login` → username + password tekshiriladi
2. `AuthService.LoginAsync()`:
   - User ni topadi (username boʻyicha)
   - `PasswordHasher.Verify()` qiladi
   - `GenerateTokensAsync()`:
     - JWT access token (15 min) — ichiga `perm` claim'lari yoziladi
     - Refresh token (7 kun) — hash'lanib DB'ga saqlanadi, HttpOnly cookie'ga yoziladi
3. JWT xotirada saqlanadi (localStorage emas — XSS profilaktikasi)
4. Refresh token faqat cookie orqali yuboriladi (JavaScript koʻrolmaydi)

### 5.2 JWT tarkibi

```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "userId",
  "unique_name": "ops",
  "perm": ["tx:read", "tx:unpause"],
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Operations_Manager",
  "exp": 1234567890,
  "iss": "transfers-dashboard",
  "aud": "transfers-dashboard-ui"
}
```

### 5.3 Refresh flow

1. Frontend 401 olganida `httpClient.ts` interceptori avtomatik `POST /auth/refresh` chaqiradi
2. Refresh token cookie orqali yuboriladi
3. `AuthService.RefreshAsync()`:
   - Token hash'ini tekshiradi
   - Eski tokenni revoke qiladi
   - Yangi access token + yangi refresh token yaratadi
4. Agar refresh ham 401 qaytarsa → user Login sahifasiga yoʻnaltiriladi

---

## 6. Avtorizatsiya (AuthZ)

### 6.1 Permission asosidagi (PBAC)

Backendda `[Authorize(Policy = "permission:tx:unpause")]` atributi orqali tekshiriladi.

Mexanizm:
1. `PermissionPolicyProvider` — policy nomidan (`permission:tx:unpause`) `PermissionRequirement` yaratadi
2. `PermissionHandler` — JWT'dagi `perm` claim'larini tekshiradi
3. Agar kerakli permission userda boʻlsa → ok, aks holda 403

### 6.2 Permission manbalari

Userning effective permissionlari = **direct permissions** ∪ **role permissions**:

- `PermissionService.GetUserPermissionsAsync()`:
  1. `permissionRepo.GetRolePermissionCodesAsync(userId)` — rollar orqali
  2. `permissionRepo.GetDirectPermissionCodesAsync(userId)` — toʻgʻridan-toʻgʻri
  3. `rolePerms.Union(directPerms).Distinct().ToList()`

### 6.3 Frontend permission tekshiruvi

```typescript
// Router guard (router/index.ts):
meta: { permission: Permission.TxRead }

// Componentda:
auth.hasPermission('tx:unpause')  // true/false
```

### 6.4 Seed permissionlar

| Code | Description | Qaysi rollarda |
|---|---|---|
| `tx:read` | View transactions | Support_Level_1, Operations_Manager, Compliance_Officer |
| `tx:unpause` | Release paused transaction | Operations_Manager, Compliance_Officer |
| `tx:cancel` | Cancel a transaction | Compliance_Officer |
| `audit:read` | Read audit log | Compliance_Officer |
| `admin` | Manage users, roles, permissions | Compliance_Officer |

---

## 7. Messaging / Broker

### 7.1 Abstraksiya

```csharp
public interface ICommandPublisher
{
    Task PublishAsync<T>(T command, CancellationToken ct = default);
}

public interface IEventConsumer
{
    Task ConsumeAsync(Func<object, CancellationToken, Task> handler, CancellationToken ct = default);
}
```

### 7.2 SimulatedBroker (development)

`appsettings.Development.json` da `Kafka:Enabled = false` va `MassTransit:Enabled = false` boʻlganda ishlaydi.

Ishlash tartibi:
1. API `UnpauseCommand` ni SimulatedBroker'ga yuboradi
2. SimulatedBroker 3-5 soniya kutadi (Main App processing simulation)
3. `TransactionStatusChanged` event'ini qaytaradi
4. EventRouter event'ni kerakli `TransferEvent` ga map qiladi (masalan, `TransactionStatusChanged` → status = `RegistrationFailedRetry`)
5. EventProjector bu event'ni Dashboard DB'ga yozadi

**EventRouter mapping:**

| Kafka Event | Dashboard Event | Status |
|---|---|---|
| TransactionInitiatedEvent | TransactionUpserted | ConfirmSucceeded |
| TransactionCreditCompletedEvent | TransactionStatusChanged | CreditSucceeded |
| TransactionCreditFailedEvent | TransactionStatusChanged | CreditFailed |
| TransactionPausedEvent | TransactionStatusChanged | Paused |
| TransactionUnpausedEvent | TransactionStatusChanged | ResumedToStatus |

**Corridor mapping (MapCorridor):**
| RemitterPartner | Corridor |
|---|---|
| Tinkoff, Profee, Gazprom | "RU->UZ" |
| Unlimited, MoneyGram | "US->UZ" |
| _ (default) | "XX->UZ" |

### 7.3 Kafka / MassTransit (production)

`appsettings.json` da:
```json
{
  "Kafka": { "Enabled": true, "BootstrapServers": "...", "Topic": "..." },
  "MassTransit": { "Enabled": true }
}
```

`Kafka:Enabled = true` boʻlganda:
- `KafkaCommandPublisher` ishlatiladi
- Konsumer `BackgroundService` sifatida ishga tushadi

`MassTransit:Enabled = true` boʻlganda:
- MassTransit + Kafka rider konfiguratsiyasi
- `IdempotencyFilter` orqali dublikat eventlarni filter qilish

---

## 8. Rate Limiting

`Program.cs` da ikkala policy sozlangan:

| Policy | Limit | Partition key | Endpoints |
|---|---|---|---|
| `auth` | 10 ta / minut | Remote IP | `/auth/login`, `/auth/refresh`, `/auth/logout` |
| `mutations` | 20 ta / minut | User ID (yoki IP) | Barcha POST/PUT/DELETE endpointlar |

Konfiguratsiya `appsettings.json` orqali:
```json
"RateLimiting": {
  "Auth": { "PermitLimit": 10, "WindowMinutes": 1 },
  "Mutations": { "PermitLimit": 20, "WindowMinutes": 1 }
}
```

Frontend `TransactionDetailView.vue` da 429 xatosi handler qilingan:
```typescript
ax.response?.status === 429
  ? 'Rate limit reached for mutations. Try again shortly.'
  : ax.response?.data?.message ?? 'Unpause failed'
```

---

## 9. Idempotency

### 9.1 IdempotencyFilter (MassTransit)

`IdempotencyFilter` Kafka eventlarini qayta ishlashda dublikatlarni oldini oladi:

1. `Idempotency-Key` headerdan (yoki `MessageId` / random GUID) idempotency key olinadi
2. `ProcessedMessage` jadvaliga yoziladi
3. Agar avval yozilgan boʻlsa, event skip qilinadi

### 9.2 Event idempotency

`TransactionStatusHistory.event_id` — unique constraint orqali dublikat history yozuvlari oldini olinadi.

---

## 10. Global Exception Handling

`ExceptionHandlingMiddleware` quyidagi xatolarni ushlaydi:

| Exception turi | HTTP Status | Javob |
|---|---|---|
| `ArgumentException` | 400 | `{ message: "..." }` |
| `InvalidOperationException` | 400 | `{ message: "..." }` |
| `NotFoundException` | 404 | `{ message: "..." }` |
| `SecurityTokenExpiredException` | 401 | `{ message: "Token expired" }` |
| Boshqa barcha | 500 | `{ message: "An unexpected error occurred" }` |

Development muhitida 500 xatolarning `detail` fieldida stack trace qaytariladi.

---

## 11. Validation

`ValidationFilter` — har bir controller action'da `ModelState.IsValid` tekshiruvi.

`AddControllers(options => options.Filters.Add<ValidationFilter>())` orqali global registratsiya qilingan.

---

## 12. JWT SigningKey Validation

`Program.cs` da startup'da tekshiriladi:
```csharp
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("JWT SigningKey must be at least 32 characters.");
```

Agar `SigningKey` 32 belgidan kam yoki placeholder boʻlsa, app ishga tushmaydi — bu production'da xavfsizlikni ta'minlaydi.

---

## 13. Frontend Muhim Detallar

### 13.1 httpClient (axios)

`src/infrastructure/httpClient.ts`:
- Base URL: `VITE_API_BASE_URL` env var (development'da `/api`, proxy orqali backend'ga yoʻnaltiriladi)
- `withCredentials: true` — refresh cookie'ni avtomatik yuboradi
- Request interceptor: access token ni `Authorization: Bearer` header'iga qoʻyadi
- Response interceptor: 401 boʻlsa → refresh qiladi → soʻrovni qayta yuboradi

### 13.2 Vite Proxy

`vite.config.ts`:
```typescript
'/api': {
  target: 'http://localhost:5290',
  changeOrigin: true,
  rewrite: (path) => path.replace(/^\/api/, ''),
}
```

Frontend `/api/auth/login` ga soʻrov yuboradi → Vite `/auth/login` ga rewrite qiladi → backend'ga yoʻnaltiradi.

### 13.3 Auth Store (Pinia)

`src/stores/auth.ts`:
- `bootstrap()` — sahifa yuklanganda `GET /auth/me` orqali sessiyani tiklaydi
- `login()` — access token ni xotirada saqlaydi
- `refresh()` — access token yangilaydi
- `logout()` — tokenlarni tozalaydi
- `hasPermission(perm)` — `user.permissions.includes(perm)` tekshiruvi

### 13.4 Admin UI

| View | Route | Description |
|---|---|---|
| UsersView | `/admin/users` | Userlar roʻyxati + Create user (ro'l tanlash bilan) |
| UserDetailView | `/admin/users/:id` | User detail, role assignment, direct permission assignment |
| RolesView | `/admin/roles` | Rollar roʻyxati + Create role |
| RoleDetailView | `/admin/roles/:id` | Role detail, permission assignment |
| PermissionsView | `/admin/permissions` | Permissionlar roʻyxati + CRUD |

---

## 14. Ishga tushirish

### 14.1 Requirementlar

- .NET 10 SDK
- Node.js 20+
- SQL Server (Mahalliy yoki Docker)

### 14.2 Backend

```bash
cd backend/src/Api
dotnet run
# API → http://localhost:5290
# Swagger → http://localhost:5290/swagger
```

### 14.3 Frontend

```bash
cd frontend
npm install
npm run dev
# UI → http://localhost:5173
```

### 14.4 Demo akkauntlar

| Username | Password | Role | Permissionlar |
|---|---|---|---|
| `support` | `Passw0rd!` | Support_Level_1 | `tx:read` |
| `ops` | `Passw0rd!` | Operations_Manager | `tx:read`, `tx:unpause` |
| `compliance` | `Passw0rd!` | Compliance_Officer | `tx:read`, `tx:unpause`, `tx:cancel`, `audit:read`, `admin` |

Parol `appsettings.Development.json` → `SeedData:DemoPassword` da sozlanadi.

### 14.5 Port avtomatik tozalash

`Program.cs` da port 5290 ni avtomatik boʻshatish logikasi bor:
```csharp
if (OperatingSystem.IsWindows())
{
    // Get-NetTCPConnection orqali eski processni topib, o'chiradi
}
```

Shuning uchun `dotnet run` har doim ishlaydi — "address already in use" xatosi chiqmaydi.

---

## 15. Konfiguratsiya

### 15.1 appsettings.json (production defaults)

| Key | Value | Izoh |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | — | SQL Server connection string (production'da env var) |
| `Jwt:SigningKey` | — | 32+ belgi (production'da env var) |
| `Jwt:Issuer` | `transfers-dashboard` | |
| `Jwt:Audience` | `transfers-dashboard-ui` | |
| `SeedData:DemoPassword` | — | Demo parol (production'da env var) |
| `Cors:AllowedOrigins` | `["http://localhost:5173"]` | Frontend origin |
| `RateLimiting:Auth:PermitLimit` | 10 | Auth endpoint limit |
| `RateLimiting:Mutations:PermitLimit` | 20 | Mutation endpoint limit |
| `Kafka:Enabled` | false | Development'da false |
| `MassTransit:Enabled` | false | Development'da false |

### 15.2 appsettings.Development.json

Sensitive qiymatlar (DB connection string, JWT kaliti, demo parol) faqat Development faylida.

### 15.3 Production deployment

Production'da sensitive qiymatlar **environment variable** yoki **secret manager** orqali beriladi:
```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;..."
export Jwt__SigningKey="your-32-char-min-key-here..."
export SeedData__DemoPassword="..."
```

---

## 16. Loyiha tuzilishi (toʻliq)

```
Transfers/
├── backend/
│   ├── Transfers.sln
│   ├── src/
│   │   ├── Domain/
│   │   │   └── Universal.Transfers.Domain.csproj
│   │   ├── Application/
│   │   │   └── Universal.Transfers.Application.csproj
│   │   ├── Infrastructure/
│   │   │   └── Universal.Transfers.Infrastructure.csproj
│   │   └── Api/
│   │       ├── Universal.Transfers.Api.csproj
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       ├── Properties/launchSettings.json
│   │       ├── Auth/
│   │       │   ├── AuthController.cs
│   │       │   ├── JwtOptions.cs
│   │       │   ├── PermissionPolicyProvider.cs
│   │       │   ├── PermissionHandler.cs
│   │       │   └── RateLimitOptions.cs
│   │       ├── Admin/
│   │       │   └── AdminController.cs
│   │       ├── Transactions/
│   │       │   └── TransactionsController.cs
│   │       ├── Audit/
│   │       │   └── AuditController.cs
│   │       └── Common/
│   │           ├── Filters/ValidationFilter.cs
│   │           └── Middlewares/ExceptionHandlingMiddleware.cs
├── frontend/
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig*.json
│   ├── index.html
│   ├── .env.development
│   ├── .env.production
│   └── src/
│       ├── main.ts
│       ├── App.vue
│       ├── domain/
│       │   ├── models.ts
│       │   ├── permissions.ts
│       │   └── paging.ts
│       ├── infrastructure/
│       │   └── httpClient.ts
│       ├── repositories/
│       │   ├── contracts.ts
│       │   ├── adminRepository.ts
│       │   ├── authRepository.ts
│       │   ├── transactionRepository.ts
│       │   └── auditRepository.ts
│       ├── services/
│       │   ├── contracts.ts
│       │   ├── adminService.ts
│       │   ├── authService.ts
│       │   ├── transactionService.ts
│       │   └── auditService.ts
│       ├── di/
│       │   └── container.ts
│       ├── stores/
│       │   └── auth.ts
│       ├── composables/
│       │   ├── useTransactions.ts
│       │   └── useAudit.ts
│       ├── components/
│       │   ├── AppHeader.vue
│       │   ├── StatusBadge.vue
│       │   └── PaginationBar.vue
│       └── views/
│           ├── LoginView.vue
│           ├── TransactionsView.vue
│           ├── TransactionDetailView.vue
│           ├── AuditView.vue
│           ├── UsersView.vue
│           ├── UserDetailView.vue
│           ├── RolesView.vue
│           ├── RoleDetailView.vue
│           ├── PermissionsView.vue
│           └── ForbiddenView.vue
├── docs/
│   ├── FULL-DOCUMENTATION.md
│   ├── architecture.md
│   ├── database-schema.md
│   └── IMPLEMENTATION-STEPS.md
└── README.md
```

---

## 17. Development Workflow

### 17.1 Yangi feature qoʻshish tartibi

1. **Domain** — entity, interface, enum (agar kerak boʻlsa)
2. **Application** — DTO, Service (business logic), interfeys
3. **Infrastructure** — Repository implementatsiyasi, EF Core config
4. **Api** — Controller, endpoint, DI registration
5. **Frontend**
   - `domain/models.ts` — TypeScript modeli
   - `repositories/contracts.ts` + `adminRepository.ts` — API call
   - `services/contracts.ts` + `adminService.ts` — Service (agar murakkab logika boʻlsa)
   - View — UI

### 17.2 Build

```bash
# Backend
cd backend
dotnet build

# Frontend
cd frontend
npm run build
```

### 17.3 Test

```bash
# API test (health)
curl http://localhost:5290/health

# Login test
curl -X POST http://localhost:5290/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"ops","password":"Passw0rd!"}'
```

---

## 18. Muhim Qarorlar (Decision Log)

| # | Qaror | Sabab |
|---|---|---|
| 1 | Record'lar (DTO) property nomlari PascalCase C# → camelCase JSON (`JsonSerializerOptions`) | .NET default JSON serializatsiyasi |
| 2 | `MapInboundClaims = false` | .NET default claim mapping'ini oʻchirish — aks holda `perm` claim'i `http://schemas...` ga aylanib ketadi |
| 3 | Permission tekshiruvi string asosida (`[Authorize(Policy = "permission:tx:read")]`) | Rol nomi emas, permission string tekshiriladi — bu RBAC + PBAC gibridini ta'minlaydi |
| 4 | Refresh token DB'da hash bilan saqlanadi | Xavfsizlik — DB'ga kirgan attacker tokenlarni koʻrolmaydi |
| 5 | Access token xotirada (localStorage emas) | XSS dan himoya |
| 6 | `[]` dan `List<>` ga oʻzgartirilmadi (collection expression) | Kod ixchamligi |
| 7 | `SimulatedBroker` development'da ishlaydi, production'da Kafka/MassTransit | Tashqi broker'siz ham toʻliq flow'ni sinash mumkin |
| 8 | JWT SigningKey startup'da tekshiriladi | Production'da placeholder kalit bilan ishga tushmaslik |
| 9 | Rate limiting endpoint atributlari orqali (`[EnableRateLimiting]`) | Policy'ni endpoint darajasida boshqarish oson |
| 10 | `VITE_API_BASE_URL=/api` + Vite proxy `/api` → backend route'ga rewrite | Development'da CORS muammosi yoʻq, production'da reverse proxy orqali |
| 11 | `UserPermission` entity si (direct permissions) allaqachon Domain'da bor edi | PermissionService `GetDirectPermissionCodesAsync` orqali JWT'ga qoʻshadi, faqat admin CRUD va frontend UI yoʻq edi |
| 12 | `Program.cs` da port avtomatik tozalash (PowerShell orqali) | Windows'da "address already in use" xatosini bartaraf qilish |

---

## 19. Xavfsizlik Eslatmalari

1. **JWT SigningKey** — production'da 32+ belgili random string, environment variable orqali
2. **Refresh token** — HttpOnly cookie, JavaScript koʻrolmaydi
3. **Access token** — faqat xotirada, localStorage/ sessionStorage yoʻq
4. **Password** — PBKDF2 (SHA-256) bilan hash'lanadi, hech qachon ochiq saqlanmaydi
5. **CORS** — faqat ruxsat etilgan originlardan soʻrov qabul qiladi
6. **Rate limiting** — auth endpointlariga 10/min, mutation'larga 20/min
7. **Audit** — barcha write amallari immutable tarzda log'lanadi
8. **Secretlar** — production'da appsettings.json bo'sh, faqat env var/secret manager

---

## 20. Ma'lum cheklovlar (Known Limitations)

1. `SimulatedBroker` production'da ishlatilmaydi — real Kafka/MassTransit kerak
2. Direct permission'lar faqat admin UI orqali boshqariladi, role orqali emas
3. Create user paytida role tanlash mumkin, permission tanlash hozircha yoʻq (faqat user detail'da)
4. Batch operatsiyalar (masalan, bir nechta userga bir vaqtda role berish) yoʻq
5. Soft delete faqat user'da (`IsActive = false`), role/permission'da hard delete
6. Transaction status enum'da barcha mumkin boʻlgan statuslar bor, lekin ba'zilari hozircha ishlatilmaydi
7. UI faqat oʻzbek/rus tilida (i18n yoʻq)
8. Testlar hozircha yoʻq (future work)
