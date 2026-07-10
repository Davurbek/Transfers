using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;
using Universal.Transfers.Domain.Transactions.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;
using Universal.Transfers.Infrastructure.Transactions.Messaging;
using Universal.Transfers.Infrastructure.Transactions.Persistence;
using Universal.Transfers.Api.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var log = new SimpleLogger();
int passed = 0, failed = 0;
void Check(bool ok, string msg) { if (ok) { log.Info($"  PASS: {msg}"); passed++; } else { log.Error($"  FAIL: {msg}"); failed++; } }

try
{
    log.Info("╔══════════════════════════════════════════╗");
    log.Info("║   E2E: EventProjector + Unpause         ║");
    log.Info("╚══════════════════════════════════════════╝");

    // ── 1. Real PostgreSQL database ──────────────────
    var conn = "Host=localhost;Database=Transfer_E2E_Test;Username=postgres";
    var opts = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
    var db = new AppDbContext(opts);
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    log.Info("Step 1: Database created");

    ITransactionRepository repo = new TransactionRepository(db);
    var projector = new EventProjector(repo, NullLogger<EventProjector>.Instance);
    var txRef = "TX-E2E-001";

    // ── 2. TransactionInitiatedEvent ─────────────────
    log.Info("Step 2: InitiatedEvent");
    await projector.ProjectAsync(new TransactionInitiatedEvent(txRef, "P-001", "standard", "tinkoff", "uzcard", 1500m, "USD", "1234", DateTime.UtcNow), default);
    var tx = await repo.GetByInternalRefAsync(txRef);
    Check(tx?.Amount == 1500m && tx?.InternalRef == txRef, "InitiatedEvent -> correct Amount & InternalRef");

    // ── 3. TransactionCreditCompletedEvent ───────────
    log.Info("Step 3: CreditCompletedEvent");
    await projector.ProjectAsync(new TransactionCreditCompletedEvent(txRef, 1, DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.CreditAttempts.Count == 1 && tx?.CurrentStatus == TransactionStatus.CreditSucceeded, "CreditCompletedEvent -> attempt=1, status=CreditSucceeded");

    // ── 4. TransactionCreditFailedEvent ──────────────
    log.Info("Step 4: CreditFailedEvent");
    await projector.ProjectAsync(new TransactionCreditFailedEvent(txRef, "P-001", 3, "Insufficient funds", DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.CurrentStatus == TransactionStatus.CreditFailed, "CreditFailedEvent -> status=CreditFailed");

    // ── 5. TransactionCreditFailedRetryEvent ─────────
    log.Info("Step 5: CreditFailedRetryEvent");
    await projector.ProjectAsync(new TransactionCreditFailedRetryEvent(txRef, 2, "Timeout", DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.CreditAttempts.Count == 3 && tx?.CurrentStatus == TransactionStatus.CreditFailedRetry, "CreditFailedRetryEvent -> 3 attempts total, status=CreditFailedRetry");

    // ── 6. TransactionCreditRetryRequestedEvent ──────
    log.Info("Step 6: CreditRetryRequestedEvent");
    await projector.ProjectAsync(new TransactionCreditRetryRequestedEvent(txRef, DateTime.UtcNow), default);
    Check(true, "CreditRetryRequestedEvent -> accepted");

    // ── 7. TransactionPausedEvent ────────────────────
    log.Info("Step 7: PausedEvent");
    await projector.ProjectAsync(new TransactionPausedEvent(txRef, "CreditFailure", null, TransactionStatus.CreditFailedRetry, DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.IsPaused == true && tx?.CurrentStatus == TransactionStatus.Paused, "PausedEvent -> IsPaused=true, status=Paused");

    // ── 8. TransactionUnpausedEvent ──────────────────
    log.Info("Step 8: UnpausedEvent (direct)");
    await projector.ProjectAsync(new TransactionUnpausedEvent(txRef, TransactionStatus.CreditFailedRetry, DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.IsPaused == false && tx?.CurrentStatus == TransactionStatus.CreditFailedRetry, "UnpausedEvent -> IsPaused=false, status=CreditFailedRetry");

    // ── 9. TransactionRegistrationCompletedEvent ─────
    log.Info("Step 9: RegistrationCompletedEvent");
    await projector.ProjectAsync(new TransactionRegistrationCompletedEvent(txRef, "tinkoff", 1, DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.PartnerRegistrations.Count == 1 && tx?.CurrentStatus == TransactionStatus.RegistrationSucceeded, "RegistrationCompletedEvent -> reg=1, status=RegistrationSucceeded");

    // ── 10. TransactionRegistrationFailedRetryEvent ──
    log.Info("Step 10: RegistrationFailedRetryEvent");
    await projector.ProjectAsync(new TransactionRegistrationFailedRetryEvent(txRef, "profee", 1, DateTime.UtcNow.AddHours(1), "Partner unavailable", DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(txRef);
    Check(tx?.PartnerRegistrations.Count == 2 && tx?.CurrentStatus == TransactionStatus.RegistrationFailedRetry, "RegistrationFailedRetryEvent -> 2 registrations, status=RegistrationFailedRetry");

    // ── 11. TransactionRegistrationRetryRequestedEvent
    log.Info("Step 11: RegistrationRetryRequestedEvent");
    await projector.ProjectAsync(new TransactionRegistrationRetryRequestedEvent(txRef, DateTime.UtcNow), default);
    Check(true, "RegistrationRetryRequestedEvent -> accepted");

    // ══════════════════════════════════════════════════
    // UNPAUSE FLOW TEST
    // ══════════════════════════════════════════════════
    log.Info("");
    log.Info("Step 12: Setting up paused transaction for Unpause...");
    var uxRef = "TX-E2E-UNPAUSE";
    await projector.ProjectAsync(new TransactionInitiatedEvent(uxRef, null, "standard", "moneygram", "humo", 500m, "EUR", "5678", DateTime.UtcNow), default);
    await projector.ProjectAsync(new TransactionPausedEvent(uxRef, "RegistrationFailure", null, TransactionStatus.RegistrationFailedRetry, DateTime.UtcNow), default);
    tx = await repo.GetDetailByInternalRefAsync(uxRef);
    Check(tx?.IsPaused == true, "Paused transaction created");

    log.Info("Step 13: SimulatedBroker Unpause...");
    var sp = new DiStub(repo, projector);
    var cfg = new CfgStub();
    var broker = new SimulatedBroker(sp, NullLogger<SimulatedBroker>.Instance, cfg);
    var brokerTask = broker.StartAsync(default);
    await broker.PublishAsync(new UnpauseTransactionCommand(uxRef, "e2e-test"), default);
    await Task.Delay(5000);
    await broker.StopAsync(default);

    tx = await repo.GetDetailByInternalRefAsync(uxRef);
    Check(tx is not null && !tx.IsPaused && tx.CurrentStatus == TransactionStatus.RegistrationSucceeded,
        $"Unpause flow -> IsPaused={tx?.IsPaused}, status={tx?.CurrentStatus} (expected false, RegistrationSucceeded)");

    var statusCount = tx?.StatusHistory?.Count ?? 0;
    Check(statusCount >= 4, $"Unpause flow -> StatusHistory entries={statusCount} (expected >=4)");

    // ══════════════════════════════════════════════════
    // SUMMARY
    // ══════════════════════════════════════════════════
    log.Info("");
    log.Info($"RESULTS: {passed} passed, {failed} failed");

    await db.Database.EnsureDeletedAsync();
    log.Info("Test database deleted.");

    Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : $"SOME TESTS FAILED ({failed})");
    Environment.Exit(failed == 0 ? 0 : 1);
}
catch (Exception ex)
{
    log.Error($"FATAL: {ex}");
    Console.Error.WriteLine($"FATAL: {ex.Message}");
    Environment.Exit(1);
}

// ─── Helpers ──────────────────────────────────────────────
class SimpleLogger { public void Info(string m) => Console.WriteLine($"[INFO] {m}"); public void Error(string m) => Console.WriteLine($"[ERR]  {m}"); }

class DiStub(ITransactionRepository repo, IEventProjector projector) : IServiceProvider, IServiceScopeFactory
{
    public object? GetService(Type t) =>
        t == typeof(IServiceScopeFactory) ? this :
        t == typeof(ITransactionRepository) ? repo :
        t == typeof(IEventProjector) ? projector :
        null;
    public IServiceScope CreateScope() => new ScopeStub(this);
    class ScopeStub(IServiceProvider sp) : IServiceScope { public IServiceProvider ServiceProvider => sp; public void Dispose() { } }
}

class CfgStub : IConfiguration
{
    public string? this[string k] { get => k == "SimulatedBroker:DelayMilliseconds" ? "500" : null; set { } }
    public IConfigurationSection GetSection(string k) => new SectStub(k);
    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
}

class SectStub(string key) : IConfigurationSection
{
    public string? this[string k] { get => k == "DelayMilliseconds" ? "500" : null; set { } }
    public string Key => key;
    public string Path => key;
    public string? Value { get => null; set { } }
    public IConfigurationSection GetSection(string k) => this;
    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
}
