# Transfers — Operatsion Dashboard

## To'liq foydalanish qo'llanmasi (O'zbek tilida)

---

## Mundarija

1. [Loyiha haqida](#1-loyiha-haqida)
2. [Arxitektura](#2-arxitektura)
3. [O'rnatish va ishga tushirish](#3-ornatish-va-ishga-tushirish)
4. [Backend tuzilishi](#4-backend-tuzilishi)
5. [Frontend tuzilishi](#5-frontend-tuzilishi)
6. [API endpointlar](#6-api-endpointlar)
7. [Ma'lumotlar bazasi sxemasi](#7-malumotlar-bazasi-sxemasi)
8. [Autentifikatsiya va avtorizatsiya](#8-autentifikatsiya-va-avtorizatsiya)
9. [Messaging / Kafka](#9-messaging--kafka)
10. [Foydalanish bo'yicha qo'llanma](#10-foydalanish-boyicha-qollanma)
11. [Muhim tushunchalar](#11-muhim-tushunchalar)
12. [Xatolarni bartaraf qilish](#12-xatolarni-bartaraf-qilish)

---

## 1. Loyiha haqida

**Transfers Operational Dashboard** — bu xalqaro pul o'tkazmalari (remittance) xizmati uchun mo'ljallangan **o'qishga mo'ljallangan** (read-mostly) operatsion panel. Loyiha Clean Architecture asosida qurilgan bo'lib, quyidagi texnologiyalardan foydalanadi:

| Komponent | Texnologiya |
|-----------|-------------|
| **Backend** | .NET 10 / ASP.NET Core |
| **Frontend** | Vue 3 + TypeScript + Vite |
| **Ma'lumotlar bazasi** | SQL Server / PostgreSQL |
| **Messaging** | Apache Kafka (yoki MassTransit) |
| **Auth** | JWT (access + refresh token) |

### Asosiy imkoniyatlar

| Imkoniyat | Turi | Izoh |
|-----------|------|------|
| Tranzaksiyalar ro'yxati va qidiruv | O'qish | `transaction_id`, `user_id`, `timestamp` bo'yicha indekslangan |
| Status o'zgarishlar tarixi | O'qish | To'liq vaqt tamg'ali audit trail |
| Credit urinishlar tarixi | O'qish | Gateway javoblari va xato kodlari |
| Partner registratsiya tarixi | O'qish | Hamkor tarmoq natijalari |
| **Tranzaksiyani unpause qilish** | Yozish | Main App'ga command yuboradi |
| Autentifikatsiya | — | JWT access token + refresh token (HttpOnly cookie) |
| Avtorizatsiya | — | RBAC + PBAC gibridi |
| Audit logging | — | Har bir yozish amali immutable qayd etiladi |
| Rate limiting | — | Auth va mutation endpointlarda qat'iy cheklov |

---

## 2. Arxitektura

### 2.1 Umumiy arxitektura

```
┌─────────────────┐         Kafka events          ┌──────────────────┐
│   Main App      │  ──────────────────────────▶   │  Dashboard API   │
│  (source of     │                                │  (.NET 10)       │
│   truth)        │  ◀──────────────────────────   │  + Dashboard DB  │
│                 │       Kafka commands           └────────┬─────────┘
└─────────────────┘                                         │
                                                     JWT / REST
                                                            │
                                                     ┌──────▼──────┐
                                                     │ Dashboard UI│
                                                     │  (Vue 3)    │
                                                     └─────────────┘
```

### 2.2 Ma'lumot oqimi (Read Path)

```
Main App                Kafka (events)              Dashboard API           Dashboard DB
   │                          │                           │                       │
   │  TransactionUpserted     │                           │                       │
   │  TransactionStatusChanged│                           │                       │
   ├─────────────────────────▶│                           │                       │
   │                          │  consume (KafkaEventConsumer)                     │
   │                          ├─────────────────────────▶  │                       │
   │                          │                           │  EventProjector       │
   │                          │                           ├──────────────────────▶│
   │                          │                           │  upsert transaction   │
   │                          │                           │  append history       │
```

### 2.3 Buyruq oqimi (Write Path) — Unpause

```
UI                Dashboard API              Kafka (commands)        Main App
 │  POST /unpause        │                         │                     │
 ├──────────────────────▶│  authorize tx:unpause   │                     │
 │                       │  write AuditLog         │                     │
 │                       │  publish UnpauseCommand │                     │
 │                       ├────────────────────────▶│  consume command    │
 │                       │                         ├────────────────────▶│
 │    202 Accepted       │                         │                     │
 │◀──────────────────────┤                         │  TxStatusChanged    │
 │                       │  consume event          │◀────────────────────┤
 │  poll / refresh       │  update Dashboard DB    │                     │
```

### 2.4 Backend layering

```
Api (Controller)
  │  depends on
  ▼
Application (Service / BLL)
  │  depends on interfaces
  ▼
Infrastructure (Repository / DAL)
  │
  ▼
Domain (Entities, Enums — hech qanday dependency'siz)
```

### 2.5 Frontend layering

```
View / Component
  │
  ▼
Composable / Store (Pinia)
  │
  ▼
Service (BLL)
  │
  ▼
Repository (DAL)
  │
  ▼
httpClient (Axios) → API
```

---

## 3. O'rnatish va ishga tushirish

### 3.1 Talablar

| Dastur | Versiya |
|--------|---------|
| .NET SDK | 10.0+ |
| Node.js | 22+ |
| SQL Server | 2019+ yoki PostgreSQL 15+ |
| Kafka (ixtiyoriy) | 3.5+ |

### 3.2 Backendni ishga tushirish

```bash
cd backend

# Solution'ni build qilish
dotnet build Transfers.Dashboard.sln

# API'ni ishga tushirish
cd src/Api
dotnet run
```

- API manzili: `http://localhost:5290`
- Swagger UI: `http://localhost:5290/swagger`
- **Birinchi ishga tushganda** ma'lumotlar bazasi avtomatik yaratiladi va demo ma'lumotlar bilan to'ldiriladi.

### 3.3 Frontendni ishga tushirish

```bash
cd frontend

# Paketlarni o'rnatish
npm install

# Development rejimida ishga tushirish
npm run dev
```

- UI manzili: `http://localhost:5173`
- Development server `/api` so'rovlarini backend'ga proksi qiladi (vite.config.ts).

### 3.4 Docker orqali (Kafka)

```bash
cd ..
docker-compose up -d
```

Bu Kafka + Zookeeper'ni ishga tushiradi.

### 3.5 Konfiguratsiya

Asosiy sozlamalar `backend/src/Api/appsettings.json` va `appsettings.Development.json` fayllarida joylashgan.

**Muhim sozlamalar:**

| Parametr | Tavsif | Default |
|----------|--------|---------|
| `ConnectionStrings:Dashboard` | Ma'lumotlar bazasi ulanish satri | — |
| `Jwt:SigningKey` | JWT imzolash kaliti (min 32 belgi) | — |
| `Jwt:Issuer` | Token chiqaruvchi | `transfers-dashboard` |
| `Jwt:AccessTokenMinutes` | Access token amal qilish muddati | 15 daqiqa |
| `Cors:AllowedOrigins` | Ruxsat etilgan frontend manzillar | `["http://localhost:5173"]` |
| `SeedData:DemoPassword` | Demo foydalanuvchilar paroli | `Passw0rd!` |
| `Kafka:Enabled` | Kafka yoqilgan/yoniq | `false` |
| `MassTransit:Enabled` | MassTransit yoqilgan/yoniq | `false` |

### 3.6 Demo akkauntlar

| Foydalanuvchi | Parol | Rol | Ruxsatlar |
|---------------|-------|-----|-----------|
| `support` | `Passw0rd!` | Support_Level_1 | `tx:read` |
| `ops` | `Passw0rd!` | Operations_Manager | `tx:read`, `tx:unpause` |
| `compliance` | `Passw0rd!` | Compliance_Officer | `tx:read`, `tx:unpause`, `tx:cancel`, `audit:read` |

---

## 4. Backend tuzilishi

### 4.1 Loyiha tuzilishi

```
backend/
├── Transfers.Dashboard.sln
├── Directory.Build.props              # Umumiy build sozlamalari
├── Directory.Packages.props           # Markazlashtirilgan paket versiyalari
├── src/
│   ├── Domain/                        # Entity'lar, enum'lar (hech qanday dependency'siz)
│   │   ├── Domain.csproj
│   │   ├── Auth/
│   │   │   ├── Entities/              # User, Role, Permission, RefreshToken, etc.
│   │   │   ├── Interfaces/            # IUserRepository, IAdminRepository, etc.
│   │   │   └── Permissions.cs         # Ruxsat satrlari katalogi
│   │   ├── Audit/
│   │   │   ├── Entities/AuditLog.cs
│   │   │   └── Interfaces/IAuditRepository.cs
│   │   ├── Transactions/
│   │   │   ├── Entities/              # Transaction, CreditAttempt, etc.
│   │   │   ├── Enums/                 # TransactionStatus, CreditGateway, OperationResult
│   │   │   └── Interfaces/ITransactionRepository.cs
│   │   ├── Inbox/
│   │   │   └── Entities/ProcessedMessage.cs
│   │   └── Common/
│   │       ├── PagedQuery.cs, PagedResult.cs
│   │       ├── TransactionFilter.cs, AuditFilter.cs
│   │       ├── Exceptions/DomainException.cs
│   │       └── Security/PasswordHasher.cs
│   │
│   ├── Application/                   # Biznes logika (Services, DTOs)
│   │   ├── Application.csproj
│   │   ├── DependencyInjection.cs     # DI registratsiya
│   │   ├── Auth/
│   │   │   ├── AuthService.cs         # Login, refresh, logout
│   │   │   ├── TokenService.cs        # JWT token yaratish
│   │   │   ├── PermissionService.cs   # Ruxsatlarni yig'ish
│   │   │   ├── JwtOptions.cs
│   │   │   └── DTOs/AuthDtos.cs
│   │   ├── Admin/
│   │   │   ├── AdminService.cs        # Foydalanuvchi/rol/ruxsat CRUD
│   │   │   ├── IAdminService.cs
│   │   │   └── DTOs/AdminDtos.cs
│   │   ├── Transactions/
│   │   │   ├── TransactionService.cs  # Tranzaksiyalarni qidirish, unpause
│   │   │   ├── ITransactionService.cs
│   │   │   ├── DTOs/TransactionDtos.cs
│   │   │   └── Mappings/TransactionMappings.cs
│   │   ├── Audit/
│   │   │   ├── AuditService.cs
│   │   │   ├── IAuditService.cs
│   │   │   └── DTOs/AuditDtos.cs
│   │   └── Messaging/
│   │       ├── Contracts.cs           # TransferCommand, TransferEvent
│   │       ├── ICommandPublisher.cs
│   │       ├── IEventConsumer.cs
│   │       └── IEventProjector.cs
│   │
│   ├── Infrastructure/                # Ma'lumotlar bazasi, messaging
│   │   ├── Infrastructure.csproj
│   │   ├── DependencyInjection.cs
│   │   ├── Common/Persistence/
│   │   │   └── AppDbContext.cs        # EF Core DbContext
│   │   ├── Auth/Persistence/
│   │   │   ├── UserRepository.cs
│   │   │   ├── AdminRepository.cs
│   │   │   ├── PermissionRepository.cs
│   │   │   └── RefreshTokenRepository.cs
│   │   ├── Transactions/
│   │   │   ├── Persistence/TransactionRepository.cs
│   │   │   └── Messaging/EventProjector.cs   # Event'larni DB'ga yozish
│   │   ├── Audit/Persistence/AuditRepository.cs
│   │   ├── Inbox/Persistence/
│   │   │   └── ProcessedMessageRepository.cs
│   │   ├── Messaging/
│   │   │   ├── Kafka/
│   │   │   │   ├── KafkaCommandConsumer.cs   # Command'larni Kafka'dan o'qish
│   │   │   │   ├── KafkaCommandPublisher.cs  # Command'larni Kafka'ga yozish
│   │   │   │   ├── KafkaEventConsumer.cs     # Event'larni Kafka'dan o'qish
│   │   │   │   └── KafkaOptions.cs
│   │   │   └── MassTransit/
│   │   │       ├── MassTransitMessagingConfiguration.cs
│   │   │       ├── MassTransitOptions.cs
│   │   │       ├── EventRouter.cs
│   │   │       ├── Consumers/MainEventConsumer.cs
│   │   │       └── Filters/IdempotencyFilter.cs
│   │   └── Seeding/DbSeeder.cs        # Demo ma'lumotlar
│   │
│   └── Api/                           # HTTP qatlam (Controllers, Middleware)
│       ├── Api.csproj
│       ├── Program.cs                 # Ilova kirish nuqtasi
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Properties/launchSettings.json
│       ├── Auth/
│       │   ├── AuthController.cs      # /auth/login, /auth/refresh, /auth/logout, /auth/me
│       │   ├── ClaimsPrincipalExtensions.cs
│       │   ├── PermissionAuthorization.cs  # Permission policy provider
│       │   └── RateLimitOptions.cs
│       ├── Admin/AdminController.cs   # Foydalanuvchi/rol/ruxsat boshqaruvi
│       ├── Transactions/TransactionsController.cs
│       ├── Audit/AuditController.cs
│       ├── Messaging/SimulatedBroker.cs    # Kafka o'rnini bosuvchi (PoC)
│       └── Common/
│           ├── Middlewares/ExceptionHandlingMiddleware.cs
│           └── Filters/ValidationFilter.cs
│
└── tests/
    └── Kafka.IntegrationTest/          # Kafka integratsiya testi
        ├── Kafka.IntegrationTest.csproj
        └── Program.cs
```

### 4.2 Muhim fayllar tavsifi

| Fayl | Vazifasi |
|------|----------|
| `Program.cs` | Ilova kirish nuqtasi. Barcha xizmatlarni ulaydi (DI, JWT, CORS, Rate Limiting, Swagger) |
| `AppDbContext.cs` | EF Core konteksti. Barcha DbSet'lar va indekslar shu yerda |
| `EventProjector.cs` | Kafka'dan kelgan event'larni DB'ga idempotent tarzda yozadi |
| `SimulatedBroker.cs` | Kafka yo'q paytida in-process broker simulyatsiyasi |
| `KafkaEventConsumer.cs` | `transfers-events` topic'ini listen qiladi, event'larni EventProjector'ga yuboradi |
| `KafkaCommandConsumer.cs` | `transfers-commands` topic'ini listen qiladi, kommandalarni bajaradi |
| `KafkaCommandPublisher.cs` | Command'larni Kafka'ga publish qiladi |
| `PasswordHasher.cs` | PBKDF2 (SHA-256) asosida parol hashlash |
| `DbSeeder.cs` | Demo ma'lumotlar bilan to'ldirish |

---

## 5. Frontend tuzilishi

### 5.1 Loyiha tuzilishi

```
frontend/
├── package.json
├── tsconfig.json
├── vite.config.ts
├── index.html
├── env.d.ts
├── src/
│   ├── main.ts                          # Vue ilova kirish nuqtasi
│   ├── App.vue                          # Asosiy layout (sidebar + router-view)
│   ├── style.css                        # Global CSS (dark theme)
│   ├── router/
│   │   └── index.ts                     # Route'lar va guard'lar
│   ├── stores/
│   │   └── auth.ts                      # Pinia auth store (login, logout, refresh, permissions)
│   ├── infrastructure/
│   │   └── httpClient.ts                # Axios instance, auth interceptor
│   ├── domain/
│   │   ├── models.ts                    # TypeScript interfeyslar
│   │   ├── permissions.ts               # Ruxsat konstantalari
│   │   └── paging.ts                    # PagedResult, filter turlari
│   ├── repositories/                    # DAL (ma'lumotlar olish qatlami)
│   │   ├── contracts.ts                 # Interfeyslar
│   │   ├── authRepository.ts
│   │   ├── transactionRepository.ts
│   │   ├── auditRepository.ts
│   │   └── adminRepository.ts
│   ├── services/                        # BLL (biznes logika)
│   │   ├── contracts.ts
│   │   ├── authService.ts
│   │   ├── transactionService.ts
│   │   ├── auditService.ts
│   │   ├── adminService.ts
│   │   └── queryUtils.ts
│   ├── di/
│   │   └── container.ts                 # DI container (composition root)
│   ├── composables/                     # Reaktiv state management
│   │   ├── useTransactions.ts
│   │   ├── useAudit.ts
│   │   └── usePermissions.ts
│   ├── components/                      # Qayta ishlatiladigan komponentlar
│   │   ├── AppHeader.vue                # Sidebar navigatsiya
│   │   ├── StatusBadge.vue              # Status rangli belgisi
│   │   ├── PaginationBar.vue            # Sahifalash paneli
│   │   └── PermissionGate.vue           # Ruxsatga qarab UI ko'rsatish
│   └── views/                           # Sahifalar
│       ├── LoginView.vue                # Kirish sahifasi
│       ├── TransactionsView.vue         # Tranzaksiyalar ro'yxati
│       ├── TransactionDetailView.vue    # Tranzaksiya detali
│       ├── AuditView.vue                # Audit log
│       ├── UsersView.vue                # Foydalanuvchilar ro'yxati
│       ├── UserDetailView.vue           # Foydalanuvchi detali
│       ├── RolesView.vue                # Rollar ro'yxati
│       ├── RoleDetailView.vue           # Rol detali
│       ├── PermissionsView.vue          # Ruxsatlar ro'yxati
│       └── ForbiddenView.vue            # 403 sahifasi
```

### 5.2 Asosiy komponentlar tavsifi

| Komponent | Vazifasi |
|-----------|----------|
| `AppHeader.vue` | Sidebar panel. Foydalanuvchi ruxsatiga qarab menyu ko'rsatadi |
| `StatusBadge.vue` | Status matnini rangli badge ko'rinishida chiqaradi (yashil=success, qizil=failed, sariq=paused) |
| `PaginationBar.vue` | Sahifalash paneli (oldingi/keyingi, sahifa hajmi) |
| `PermissionGate.vue` | Slot orqali UI ni ruxsatga qarab ko'rsatadi/yashiradi |
| `httpClient.ts` | Axios interceptor. 401 xatoda avtomatik refresh token yuboradi |

### 5.3 Route'lar

| Path | Sahifa | Ruxsat | Public |
|------|--------|--------|--------|
| `/login` | LoginView | — | Ha |
| `/` | TransactionsView | `tx:read` | Yo'q |
| `/transactions/:id` | TransactionDetailView | `tx:read` | Yo'q |
| `/audit` | AuditView | `audit:read` | Yo'q |
| `/admin/users` | UsersView | `admin` | Yo'q |
| `/admin/users/:id` | UserDetailView | `admin` | Yo'q |
| `/admin/roles` | RolesView | `admin` | Yo'q |
| `/admin/roles/:id` | RoleDetailView | `admin` | Yo'q |
| `/admin/permissions` | PermissionsView | `admin` | Yo'q |
| `/forbidden` | ForbiddenView | — | Ha |

---

## 6. API endpointlar

### 6.1 Autentifikatsiya

#### `POST /auth/login`
Tizimga kirish. JWT access token va HttpOnly refresh cookie qaytaradi.

**So'rov:**
```json
{ "username": "ops", "password": "Passw0rd!" }
```

**Javob (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "accessTokenExpiresAt": "2026-07-08T12:00:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "ops",
    "email": "ops@transfers.local",
    "permissions": ["tx:read", "tx:unpause"],
    "roles": ["Operations_Manager"]
  }
}
```

#### `POST /auth/refresh`
Refresh token orqali yangi access token olish. Cookie orqali `tfx_refresh` yuboriladi.

#### `POST /auth/logout`
Tizimdan chiqish. Refresh tokenni bekor qiladi.

#### `GET /auth/me`
Joriy foydalanuvchi ma'lumotlari.

### 6.2 Tranzaksiyalar

#### `GET /transactions`
Tranzaksiyalarni qidirish va filtrlash.

**Query parametrlari:**
| Parametr | Turi | Izoh |
|----------|------|------|
| `search` | string | Transaction ID yoki recipient bo'yicha qidiruv |
| `status` | string | Status bo'yicha filtr |
| `userId` | string | Foydalanuvchi ID bo'yicha filtr |
| `isPaused` | boolean | Pauza qilinganlar bo'yicha filtr |
| `fromDate` | datetime | Boshlanish sanasi |
| `toDate` | datetime | Tugash sanasi |
| `page` | int | Sahifa raqami (default: 1) |
| `pageSize` | int | Sahifa hajmi (default: 20, max: 100) |

**Javob (200):**
```json
{
  "items": [
    {
      "internalRef": "intref-tx-1002",
      "transactionId": "TX-1002",
      "userId": "user-77",
      "recipientName": "Dilnoza Yusupova",
      "amount": 540.50,
      "currency": "USD",
      "corridor": "RU->UZ",
      "currentStatus": "Paused",
      "isPaused": true,
      "createdAt": "2026-07-08T10:30:00Z",
      "updatedAt": "2026-07-08T11:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasPrevious": false,
  "hasNext": false
}
```

#### `GET /transactions/{id}`
Tranzaksiya detali. To'liq lifecycle, credit urinishlari va partner registratsiyalarini qaytaradi.

#### `POST /transactions/{id}/unpause`
Pauza qilingan tranzaksiyani unpause qilish. **Ruxsat talab qiladi**: `tx:unpause`

**Javob (202 - Accepted):**
```json
{
  "message": "Unpause command accepted",
  "transactionId": "TX-1002",
  "commandId": "c06e16b6-bdcc-46ef-aa80-8d770e7314f1"
}
```

### 6.3 Audit log

#### `GET /audit`
Audit yozuvlarini qidirish.

**Query parametrlari:**
| Parametr | Turi | Izoh |
|----------|------|------|
| `targetTransactionId` | string | Tranzaksiya ID bo'yicha filtr |
| `actionType` | string | Harakat turi bo'yicha filtr |
| `username` | string | Foydalanuvchi nomi bo'yicha filtr |
| `fromDate` | datetime | Boshlanish sanasi |
| `toDate` | datetime | Tugash sanasi |
| `page` | int | Sahifa raqami (default: 1) |
| `pageSize` | int | Sahifa hajmi (default: 50, max: 200) |

### 6.4 Admin (Foydalanuvchi/Rol/Ruxsat boshqaruvi)

| Method | Route | Izoh |
|--------|-------|------|
| `GET` | `/admin/users` | Barcha foydalanuvchilar |
| `GET` | `/admin/users/{id}` | Foydalanuvchi detali |
| `POST` | `/admin/users` | Yangi foydalanuvchi yaratish |
| `PUT` | `/admin/users/{id}` | Foydalanuvchini tahrirlash |
| `DELETE` | `/admin/users/{id}` | Foydalanuvchini o'chirish |
| `POST` | `/admin/users/{userId}/roles` | Foydalanuvchiga rol biriktirish |
| `DELETE` | `/admin/users/{userId}/roles/{roleId}` | Foydalanuvchidan rol olib tashlash |
| `POST` | `/admin/users/{userId}/permissions` | Foydalanuvchiga ruxsat biriktirish |
| `DELETE` | `/admin/users/{userId}/permissions/{permId}` | Foydalanuvchidan ruxsat olib tashlash |
| `GET` | `/admin/roles` | Barcha rollar |
| `GET` | `/admin/roles/{id}` | Rol detali |
| `POST` | `/admin/roles` | Yangi rol yaratish |
| `PUT` | `/admin/roles/{id}` | Rolni tahrirlash |
| `DELETE` | `/admin/roles/{id}` | Rolni o'chirish |
| `POST` | `/admin/roles/{roleId}/permissions` | Rolga ruxsat biriktirish |
| `DELETE` | `/admin/roles/{roleId}/permissions/{permId}` | Rolldan ruxsat olib tashlash |
| `GET` | `/admin/permissions` | Barcha ruxsatlar |
| `POST` | `/admin/permissions` | Yangi ruxsat yaratish |
| `PUT` | `/admin/permissions/{id}` | Ruxsatni tahrirlash |
| `DELETE` | `/admin/permissions/{id}` | Ruxsatni o'chirish |

### 6.5 Health check

| Method | Route | Izoh |
|--------|-------|------|
| `GET` | `/health` | Database ulanishini tekshiradi |
| `GET` | `/health/ready` | Ilova tayyorligini tekshiradi |
| `GET` | `/health/live` | Ilova jonliligini tekshiradi |

---

## 7. Ma'lumotlar bazasi sxemasi

### 7.1 Auth jadvallari

```
users                          roles                        permissions
─────────────────              ──────────────               ────────────────
id (PK, GUID)                  id (PK, GUID)                id (PK, GUID)
username (UQ)                  name (UQ)                    code (UQ) — masalan: tx:unpause
email                          description                  description
password_hash
is_active
created_at

user_roles (M:N)               role_permissions (M:N)       user_permissions (M:N)
─────────────────              ──────────────────────       ────────────────────────
user_id (FK -> users)          role_id (FK -> roles)        user_id (FK -> users)
role_id (FK -> roles)          permission_id (FK -> perm)   permission_id (FK -> perm)
PK(user_id, role_id)           PK(role_id, permission_id)   PK(user_id, permission_id)

refresh_tokens
─────────────────
id (PK)
user_id (FK -> users)
token_hash (UQ)          — hash saqlanadi, raw token hech qachon saqlanmaydi
expires_at
revoked_at (nullable)    — darhol bekor qilish imkoniyati
created_at
created_by_ip
```

**Effektiv ruxsatlar:**
```
user_permissions ∪ (user_roles → role_permissions)
```

### 7.2 Tranzaksiya jadvallari

```
transactions
─────────────────
id (PK, GUID)
internal_ref (UQ)                 — ichki referens
transaction_id (UQ)               — biznes ID, INDEXED
user_id                           — jo'natuvchi, INDEXED
recipient_name
amount (decimal 18,2)
currency (nvarchar 3)
corridor                          — masalan: "RU->UZ"
credit_gateway                    — Humo | Uzcard
remitter_partner
current_status                    — enum (string konvertatsiya)
is_paused
created_at                        — INDEXED
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
event_id (UQ)                     — idempotency key

credit_attempts
─────────────────
id (PK)
transaction_id (FK, INDEXED)
attempt_number
gateway                           — Humo | Uzcard
status                            — Succeeded | Failed
failure_code (nullable)
gateway_response (nullable)
attempted_at (INDEXED)
event_id (UQ)

partner_registrations
──────────────────────
id (PK)
transaction_id (FK, INDEXED)
partner_name
status                            — Succeeded | Failed
failure_reason (nullable)
reference_id (nullable)
registered_at (INDEXED)
event_id (UQ)
```

### 7.3 Tranzaksiya status enum

```
ConfirmPending
ConfirmExpired
ConfirmFailed
ConfirmSucceeded
CreditPending
CreditSucceeded
CreditFailed
CreditFailedRetry
RegistrationPending
RegistrationFailedRetry
RegistrationSucceeded
Paused
Cancelled
```

### 7.4 Audit jadvali

```
audit_logs (append-only — hech qachon o'chirilmaydi yoki tahrirlanmaydi)
─────────────────
id (PK)
user_id (FK -> users)
action_type               — masalan: "tx:unpause"
target_transaction_id
timestamp (INDEXED)
ip_address
metadata (nullable, JSON)
```

---

## 8. Autentifikatsiya va avtorizatsiya

### 8.1 JWT token oqimi

1. Foydalanuvchi `POST /auth/login` orqali username+password yuboradi
2. Server parolni tekshiradi (`PasswordHasher.Verify`)
3. Access token (15 daqiqa) + Refresh token (7 kun) yaratiladi
4. Refresh token **hash** bilan bazaga saqlanadi, raw token cookie'ga yoziladi
5. Frontend access tokenni xotirada saqlaydi, har bir so'rovda `Authorization: Bearer` header'iga qo'shadi
6. Token muddati tugaganda (401), frontend avtomatik `POST /auth/refresh` chaqiradi
7. Refresh token bir marta ishlatiladi (rotated) — eski token bekor qilinadi

### 8.2 Ruxsat tizimi

**RBAC + PBAC gibridi:**
- Foydalanuvchiga **to'g'ridan-to'g'ri** ruxsat berish mumkin (`user_permissions`)
- Foydalanuvchini **rol**ga biriktirish mumkin, rol orqali ruxsat oladi (`role_permissions`)
- Effektiv ruxsatlar = to'g'ridan-to'g'ri ∪ roldan kelgan ruxsatlar

**Barcha ruxsatlar:**
| Kod | Tavsif |
|-----|--------|
| `tx:read` | Tranzaksiyalarni ko'rish |
| `tx:unpause` | Tranzaksiyani unpause qilish |
| `tx:cancel` | Tranzaksiyani bekor qilish |
| `audit:read` | Audit loglarni ko'rish |
| `admin` | Admin panel (foydalanuvchi/rol/ruxsat boshqaruvi) |

### 8.3 Rate limiting

| Endpoint guruhi | Limit | Oyna |
|----------------|-------|------|
| Auth (`/auth/*`) | 10 ta so'rov | 1 daqiqa |
| Mutation (`unpause`, `admin`) | 20 ta so'rov | 1 daqiqa |

---

## 9. Messaging / Kafka

### 9.1 Umumiy tushuncha

Loyiha **event-driven** arxitektura asosida qurilgan. Main App (asosiy tizim) va Dashboard o'rtasida ma'lumot almashish uchun message broker (Kafka) ishlatiladi.

**Ikkita asosiy topic:**
| Topic | Yo'nalish | Mazmuni |
|-------|-----------|---------|
| `transfers-events` | Main App → Dashboard | Tranzaksiya holati o'zgarishlari |
| `transfers-commands` | Dashboard → Main App | Dashboard'dan yuborilgan buyruqlar |

### 9.2 Event turlari

| Event | Tavsif |
|-------|--------|
| `TransactionUpserted` | Tranzaksiya yaratilgan yoki yangilangan |
| `TransactionStatusChanged` | Tranzaksiya statusi o'zgargan |

### 9.3 Command turlari

| Command | Tavsif |
|---------|--------|
| `UnpauseTransactionCommand` | Pauza qilingan tranzaksiyani davom ettirish |

### 9.4 Ma'lumot oqimi

```
1. Main App tranzaksiya statusini o'zgartiradi
2. Event Kafka'ga publish qilinadi (transfers-events)
3. KafkaEventConsumer event'ni oladi
4. EventProjector.ProjectAsync() event'ni DB'ga yozadi
5. Frontend API orqali yangilangan ma'lumotlarni oladi
```

### 9.5 Unpause oqimi

```
1. Foydalanuvchi "Unpause" tugmasini bosadi
2. API tekshiradi: foydalanuvchi ruxsati bormi? (tx:unpause)
3. Audit logga yozadi
4. KafkaCommandPublisher orqali UnpauseTransactionCommand yuboradi
5. 202 Accepted qaytaradi (buyruq qabul qilindi, bajarilishi kutilmoqda)
6. KafkaCommandConsumer buyruqni oladi va qayta ishlaydi:
   a. TransactionStatusChanged event'larini publish qiladi
   b. Event'lar EventProjector orqali DB'ga yoziladi
7. Frontend polling orqali yangi statusni oladi
```

### 9.6 SimulatedBroker (PoC)

Kafka yo'q paytida `SimulatedBroker` ishlaydi. Bu in-process broker bo'lib, Main App'ni simulyatsiya qiladi:
1. `UnpauseCommand` ni qabul qiladi
2. 1.5 sekund kutadi
3. Tranzaksiyani `Paused` → `RegistrationFailedRetry` → `RegistrationSucceeded` ga o'tkazadi
4. Event'larni EventProjector'ga yuboradi

### 9.7 Kafka sozlamalari

```json
{
  "Kafka": {
    "Enabled": false,              // true qilish uchun
    "BootstrapServers": "localhost:9092",
    "CommandsTopic": "transfers-commands",
    "EventsTopic": "transfers-events",
    "GroupId": "transfers-dashboard",
    "DlqTopic": "transfers-events-dlq"    // muvaffaqiyatsiz event'lar uchun
  }
}
```

---

## 10. Foydalanish bo'yicha qo'llanma

### 10.1 Tizimga kirish

1. Brauzerda `http://localhost:5173` ni oching
2. "Transfers Ops" login sahifasi ochiladi
3. Username va parolni kiriting:
   - **Kuzatish uchun**: `support` / `Passw0rd!`
   - **Unpause qilish uchun**: `ops` / `Passw0rd!`
   - **To'liq huquq uchun**: `compliance` / `Passw0rd!`
4. "Sign in" tugmasini bosing

### 10.2 Tranzaksiyalarni ko'rish

1. Chap menyuda "Transactions" bo'limiga o'ting
2. Tranzaksiyalar jadval ko'rinishida chiqadi
3. Filtrlash uchun:
   - **Search** — Transaction ID yoki recipient nomi bo'yicha qidirish
   - **Status** — Status bo'yicha tanlash
   - **Sender ID** — Jo'natuvchi ID bo'yicha
   - **From/To** — Sana oralig'i
4. "Filter" tugmasini bosing
5. Tranzaksiya ustiga bossangiz, detal sahifasi ochiladi

### 10.3 Tranzaksiya detali

Tranzaksiya detali sahifasida:
- **Umumiy ma'lumot**: recipient, summa, valyuta, corridor, sender ID, sanalar
- **Lifecycle timeline**: status o'zgarishlarining vaqt bo'yicha ketma-ketligi
- **Credit attempts**: har bir credit urinishi (gateway, natija, xato kodi)
- **Partner registrations**: hamkor tizimlardagi registratsiyalar

### 10.4 Tranzaksiyani unpause qilish

1. Pauza qilingan tranzaksiya detali sahifasiga o'ting
2. Agar sizda `tx:unpause` ruxsati bo'lsa (ops yoki compliance), "Unpause transaction" tugmasi ko'rinadi
3. Tugmani bosing
4. "Command sent" xabari chiqadi va avtomatik polling boshlanadi
5. 1-2 soniyada status `RegistrationSucceeded` ga o'zgaradi

### 10.5 Audit log

1. Chap menyuda "Audit log" bo'limiga o'ting
2. Barcha write amallari ro'yxati (unpause, admin o'zgarishlari)
3. Filtrlash: transaction ID, action type, username, sana

### 10.6 Admin panel

**Foydalanuvchilarni boshqarish:**
1. "Users" bo'limiga o'ting
2. Yangi foydalanuvchi yaratish, tahrirlash, o'chirish
3. Foydalanuvchiga rol va ruxsat biriktirish

**Rollarni boshqarish:**
1. "Roles" bo'limiga o'ting
2. Yangi rol yaratish, tahrirlash, o'chirish
3. Rolga ruxsat biriktirish

**Ruxsatlarni boshqarish:**
1. "Permissions" bo'limiga o'ting
2. Yangi ruxsat yaratish, tahrirlash, o'chirish

---

## 11. Muhim tushunchalar

### 11.1 Clean Architecture

Loyiha Clean Architecture tamoyillariga amal qiladi:
- **Domain** — hech qanday tashqi dependency'siz (faqat entity'lar va enum'lar)
- **Application** — biznes logika, interfeyslar orqali infrastruktura bilan bog'lanadi
- **Infrastructure** — ma'lumotlar bazasi, messaging, tashqi xizmatlar
- **Api** — HTTP, middleware, controller'lar

### 11.2 CQRS (Command Query Responsibility Segregation)

- **Read path**: Event'lar Kafka orqali keladi → EventProjector → DB → API (GET) → Frontend
- **Write path**: Frontend → API (POST) → Audit log → Command yuborish → Kafka → Main App

### 11.3 Eventual Consistency (Oxirgi konsistensiya)

- Dashboard ma'lumotlari Main App bilan **asinxron** tarzda sinxronlanadi
- Event'lar idempotent (event_id orqali takroriy yozuvlarning oldi olinadi)
- Unpause kommandasi darhol bajarilmaydi, balki buyruq sifatida yuboriladi (202 Accepted)

### 11.4 Permission-based Authorization

- Ruxsat tekshiruvi **rol nomi** bilan emas, **ruxsat satri** bilan amalga oshiriladi
- Frontend'da `PermissionGate` komponenti UI elementlarini ruxsatga qarab ko'rsatadi
- Backend'da `[Authorize(Policy = "permission:tx:unpause")]` deklarativ tekshiruv

---

## 12. Xatolarni bartaraf qilish

### 12.1 Backend build xatolari

| Xato | Sabab | Yechim |
|------|-------|--------|
| `NETSDK1045: .NET 10 SDK not found` | .NET 10 o'rnatilmagan | .NET 10 SDK ni o'rnating |
| `NU1301: Unable to load the service index` | Internet yo'q yoki NuGet proxy | Internet ulanishini tekshiring |
| `Build succeeded with warnings` | EF Core MultipleCollectionInclude | .AsSplitQuery() qo'shing |

### 12.2 API runtime xatolari

| Xato | Sabab | Yechim |
|------|-------|--------|
| `JWT SigningKey must be at least 32 characters` | `Jwt:SigningKey` sozlanmagan | Kamida 32 belgili kalit qo'ying |
| `SeedData:DemoPassword is not configured` | Demo parol sozlanmagan | `appsettings.Development.json` ga qo'shing |
| `Cors:AllowedOrigins is not configured` | CORS sozlanmagan | Frontend manzilini qo'shing |
| `Cannot open database` | DB server ulanishi noto'g'ri | Connection stringni tekshiring |

### 12.3 Frontend xatolari

| Xato | Sabab | Yechim |
|------|-------|--------|
| `Failed to load resource: net::ERR_CONNECTION_REFUSED` | Backend ishlamayapti | `dotnet run` orqali backendni ishga tushiring |
| `401 Unauthorized` | Token muddati tugagan | Avtomatik refresh ishlashi kerak, yoki qayta login |
| `429 Too Many Requests` | Rate limit | Bir daqiqa kuting |
| `Module not found` | `npm install` qilinmagan | `npm install` ni bajaring |

### 12.4 Kafka xatolari

| Xato | Sabab | Yechim |
|------|-------|--------|
| `Local: Broker transport failure` | Kafka ishlamayapti | `docker-compose up -d` orqali Kafka'ni ishga tushiring |
| `Broker: Unknown topic or partition` | Topic yaratilmagan | Kafka avtomatik topic yaratish yoqilgan (`auto.create.topics.enable=true`) |
| `No such host: localhost:9092` | Kafka sozlamalari noto'g'ri | `appsettings.json` da `Kafka:BootstrapServers` ni tekshiring |

### 12.5 Ma'lumotlar bazasi xatolari

| Xato | Sabab | Yechim |
|------|-------|--------|
| `Cannot open database "Transfer"` | SQL Server ishlamayapti | SQL Server xizmatini tekshiring |
| `Login failed for user` | Autentifikatsiya xatosi | Connection string'dagi login/parolni tekshiring |
| `The INSERT statement conflicted with the CHECK constraint` | Ma'lumotlar yaxlitligi buzilgan | Event'lar idempotent emas — `event_id` unique constraint |

---

*Hujjat versiyasi: 1.0 — 2026-iyul*
