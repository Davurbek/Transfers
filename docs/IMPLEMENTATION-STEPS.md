# Bajarilgan ishlar — bosqichma-bosqich (Implementation Steps)

Ushbu hujjat "Transfers" Operatsion Dashboard loyihasini qurish jarayonida
bajarilgan **har bir qadamni** ketma-ket tushuntiradi. Maqsad — TRD asosida
.NET 10 backend API va Vue 3 frontend yaratib, `Davurbek/Transfers`
repozitoriyasiga commit qilish edi.

---

## 0. Muhitni tekshirish (Environment check)

Ishni boshlashdan oldin sandbox muhiti tekshirildi:

| Tekshiruv | Natija |
|---|---|
| `.NET` SDK | Faqat **8.0** va **9.0** mavjud (.NET 10 SDK **yo'q**) |
| Node.js | v22 mavjud |
| Tarmoq (network) | **INTEGRATIONS_ONLY** — tashqi internet yopiq |
| NuGet (`api.nuget.org`) | **403 Forbidden** — paket yuklab bo'lmaydi |
| npm (`registry.npmjs.org`) | **403 Forbidden** — paket yuklab bo'lmaydi |

**Xulosa:** kod standart .NET 10 + Vue 3 toolchainiga mo'ljab yozildi, lekin
shu sandbox'da `dotnet restore` / `npm install` ishlamaydi, shuning uchun lokal
build/test bu yerda bajarilmadi. Build foydalanuvchi mashinasida (internet bilan)
amalga oshiriladi.

---

## 1. Repozitoriyani klonlash

```text
github → repo_set_up(owner="Davurbek", repository_name="Transfers")
```

- Repozitoriya `/projects/sandbox/Transfers` ga klonlandi.
- Repozitoriya **bo'sh** edi (hech qanday commit yo'q, default branch: `master`).

---

## 2. Hujjatlar va loyiha skeleti (Docs & scaffolding)

Quyidagi fayllar yaratildi:

| Fayl | Mazmuni |
|---|---|
| `README.md` | Loyiha umumiy ko'rinishi, ishga tushirish, demo akkauntlar |
| `.gitignore` | .NET (`bin/`, `obj/`) va Node (`node_modules/`, `dist/`) uchun |
| `docs/architecture.md` | Arxitektura, read-path va write-path (command) diagrammalari |
| `docs/database-schema.md` | To'liq DB sxemasi (auth + tranzaksiya + audit jadvallari) |

---

## 3. Backend — .NET 10 / ASP.NET Core

Loyiha joylashuvi: `backend/Transfers.Dashboard.Api/`

### 3.1 Loyiha fayllari
- `Transfers.Dashboard.Api.csproj` — `net10.0` target, paketlar:
  `Microsoft.AspNetCore.Authentication.JwtBearer`,
  `Microsoft.EntityFrameworkCore.Sqlite` (+ `Design`),
  `System.IdentityModel.Tokens.Jwt`, `Swashbuckle.AspNetCore`.
- `Transfers.Dashboard.sln` — yechim (solution) fayli.

### 3.2 Domain (entity'lar)
- `Domain/Auth/AuthEntities.cs` — `User`, `Role`, `Permission`, `UserRole`,
  `RolePermission`, `UserPermission`, `RefreshToken`.
- `Domain/Transactions/TransactionEntities.cs` — `Transaction`,
  `TransactionStatusHistory`, `CreditAttempt`, `PartnerRegistration` va
  enum'lar (`TransactionStatus`, `CreditGateway`, `OperationResult`).
- `Domain/Audit/AuditLog.cs` — o'zgarmas (immutable) audit yozuvi.

### 3.3 Ma'lumotlar qatlami (Data layer)
- `Data/DashboardDbContext.cs` — EF Core konteksti; barcha indekslar shu yerda:
  `transaction_id` (unique), `user_id`, `created_at`, kompozit
  `(user_id, created_at)`, status va event'lar uchun idempotentlik indeksi.
- `Data/DbSeeder.cs` — bazani yaratadi va demo ma'lumotlar bilan to'ldiradi:
  ruxsatlar, 3 ta rol, 3 ta foydalanuvchi va 3 ta namunaviy tranzaksiya
  (jumladan, "Unpause" ni sinash uchun bitta `Paused` tranzaksiya).

### 3.4 Autentifikatsiya / Avtorizatsiya (Auth)
- `Services/PasswordHasher.cs` — PBKDF2 (SHA-256), faqat BCL, tashqi paketsiz.
- `Authorization/Permissions.cs` — ruxsat satrlari katalogi (`tx:read`,
  `tx:unpause`, `tx:cancel`, `audit:read`).
- `Auth/JwtOptions.cs` — JWT sozlamalari (issuer, audience, kalit, muddatlar).
- `Auth/PermissionResolver.cs` — **effektiv ruxsatlar = to'g'ridan-to'g'ri ∪
  rollardan kelgan** ruxsatlar.
- `Auth/TokenService.cs` — JWT access token (flattened `perm` claim'lar) +
  aylanma (rotating) refresh token (faqat hash saqlanadi, darhol bekor qilinadi).
- `Auth/ClaimsPrincipalExtensions.cs` — token'dan `userId`, `username`,
  `permissions` ni o'qish.
- `Authorization/PermissionAuthorization.cs` — `[RequirePermission("...")]`
  atributi, requirement, handler va dinamik policy provider. **Rol nomi emas,
  ruxsat satri** tekshiriladi.

### 3.5 Messaging / ma'lumot sinxronlash
- `Messaging/Contracts.cs` — event'lar (`TransactionUpserted`,
  `TransactionStatusChanged`) va buyruqlar (`UnpauseTransactionCommand`).
- `Messaging/Abstractions.cs` — `ICommandPublisher`, `IEventProjector`
  interfeyslari (broker abstraksiyasi).
- `Messaging/EventProjector.cs` — event'larni **idempotent** tarzda Dashboard
  bazasiga yozadi (read-path).
- `Messaging/SimulatedBroker.cs` — Kafka/RabbitMQ o'rnini bosuvchi PoC simulyator:
  buyruqni qabul qiladi → Main App state-machine'ini taqlid qiladi → natija
  event'ini qaytaradi → dashboard ko'rinishi yangilanadi (to'liq round-trip).

### 3.6 Servislar va Controller'lar
- `Services/AuditService.cs` — har bir write amali uchun o'zgarmas audit yozuvi.
- `Dtos/Dtos.cs` — barcha so'rov/javob DTO'lari.
- `Controllers/AuthController.cs` — `login`, `refresh`, `logout`, `me`
  (refresh token `HttpOnly; Secure; SameSite=Strict` cookie orqali).
- `Controllers/TransactionsController.cs` — ro'yxat/qidiruv, detal (lifecycle +
  credit + partner tarixi) va `unpause` (audit + command publish, `202 Accepted`).
- `Controllers/AuditController.cs` — audit log o'qish (`audit:read` talab qiladi).

### 3.7 Program.cs (hammasini ulash)
- EF Core (SQLite), JWT bearer (inbound claim mapping o'chirilgan),
  permission-based authorization, **rate limiting** (auth + mutation),
  CORS (UI alohida origin), Swagger/OpenAPI.
- Pipeline tartibi tuzatildi: `Authentication → RateLimiter → Authorization`
  (per-user rate-limit identifikatorni ko'rishi uchun).
- Startda baza yaratiladi va seed qilinadi.

### 3.8 Konfiguratsiya
- `appsettings.json`, `appsettings.Development.json`,
  `Properties/launchSettings.json`.

---

## 4. Frontend — Vue 3 + Vite + TypeScript

Loyiha joylashuvi: `frontend/`

### 4.1 Loyiha konfiguratsiyasi
- `package.json` (vue, vue-router, pinia, axios + vite, vue-tsc, typescript),
  `vite.config.ts` (`/api` → backend proxy), `tsconfig*.json`, `env.d.ts`,
  `index.html`.

### 4.2 Asosiy logika
- `src/types.ts` — TypeScript turlari + `Permission` konstantalari.
- `src/api/client.ts` — axios; access token xotirada saqlanadi, 401'da avtomatik
  `refresh` qilib so'rovni qayta yuboradi (interceptor).
- `src/api/transactions.ts` — tranzaksiya API chaqiruvlari.
- `src/stores/auth.ts` — Pinia auth store (login, refresh, logout, bootstrap,
  `hasPermission`).
- `src/router/index.ts` — route guard'lar: auth + ruxsat (`meta.permission`)
  tekshiruvi.
- `src/main.ts` — ilova kirish nuqtasi.

### 4.3 Komponentlar va sahifalar
- `components/StatusBadge.vue` — status rangli belgisi.
- `components/PermissionGate.vue` — ruxsatga qarab UI'ni ko'rsatadi/yashiradi.
- `components/AppHeader.vue` — navigatsiya (ruxsatga bog'langan menyular).
- `App.vue` — asosiy layout.
- `views/LoginView.vue` — kirish sahifasi.
- `views/TransactionsView.vue` — tranzaksiyalar ro'yxati + qidiruv.
- `views/TransactionDetailView.vue` — lifecycle timeline, credit urinishlari,
  partner registratsiyalari + **ruxsatga bog'langan Unpause** (jonli polling).
- `views/AuditView.vue` — audit log.
- `views/ForbiddenView.vue` — 403 sahifasi.

---

## 5. Tuzatilgan xato (Bug fix)

`TransactionsController.List` ichida `IQueryable.Select(t => ToSummary(t))`
ishlatilgan edi — bu EF Core'da SQL'ga **tarjima qilinmaydi** va runtime'da xato
beradi. To'g'irlandi: avval `ToListAsync()` bilan materializatsiya qilinib,
keyin xotirada `Select(ToSummary)` qilindi.

---

## 6. Git: commit va push

1. `feat/operational-dashboard` branch yaratildi.
2. Barcha fayllar `git add -A` bilan stage qilindi.
3. Tavsifli commit yozildi (identity inline `-c` flag bilan, git config
   o'zgartirilmadi).
4. Branch remote'ga push qilindi.
5. **PR ochishga urinish** `master`'ga qaratilgan edi, lekin repo bo'sh bo'lgani
   uchun (base branch mavjud emas) **422 xato** chiqdi.
6. Shu sababli commit `master` branch'iga ham push qilindi — bu repozitoriyaning
   default branch'ini o'rnatdi va kod endi repoda.

Yakuniy holat:
- Repozitoriya (master): https://github.com/Davurbek/Transfers
- Branch: https://github.com/Davurbek/Transfers/tree/feat/operational-dashboard

---

## 7. Lokal ishga tushirish (Run locally)

```bash
# Backend (.NET 10)
cd backend/Transfers.Dashboard.Api
dotnet run            # Swagger: /swagger

# Frontend (Vue 3)
cd frontend
npm install
npm run dev           # http://localhost:5173
```

**Demo akkauntlar** (parol `Passw0rd!`):

| Foydalanuvchi | Rol | Ruxsatlar |
|---|---|---|
| `support` | Support_Level_1 | `tx:read` |
| `ops` | Operations_Manager | `tx:read`, `tx:unpause` |
| `compliance` | Compliance_Officer | `tx:read`, `tx:unpause`, `tx:cancel`, `audit:read` |

---

## 8. Cheklovlar (Known limitations)

- Bu sandbox'da **.NET 10 SDK yo'q** va **internet yopiq** edi, shuning uchun kod
  bu yerda **build/test qilinmadi**. Build foydalanuvchi mashinasida bajariladi.
- Broker **simulyatsiya** qilingan (in-process). Production'da `SimulatedBroker`
  o'rniga haqiqiy Kafka/RabbitMQ klienti qo'yiladi (API/UI o'zgarmaydi).
- Lokal qulaylik uchun **SQLite** ishlatilgan; production uchun **PostgreSQL**'ga
  o'tkazish tavsiya etiladi.
