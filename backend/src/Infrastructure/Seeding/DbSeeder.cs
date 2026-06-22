using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Auth;
using Universal.Transfers.Domain.Auth.Entities;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Enums;
using Universal.Transfers.Domain.Common.Security;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string demoPassword)
    {
        await db.Database.EnsureCreatedAsync();
        await MigrateSchemaAsync(db);

        if (!await db.Permissions.AnyAsync())
            await SeedAuthAsync(db, demoPassword);

        if (!await db.Transactions.AnyAsync())
            await SeedTransactionsAsync(db);

        if (!await db.AuditLogs.AnyAsync())
            await SeedAuditAsync(db);
    }

    private static async Task MigrateSchemaAsync(AppDbContext db)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'InternalRef')
                ALTER TABLE Transactions ADD InternalRef NVARCHAR(MAX) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'IsPaused')
                ALTER TABLE Transactions ADD IsPaused BIT NOT NULL DEFAULT 0;
        ";
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            await cmd.Connection.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedAuthAsync(AppDbContext db, string demoPassword)
    {
        var permissions = Permissions.All
            .Select(kv => new Permission { Code = kv.Key, Description = kv.Value })
            .ToDictionary(p => p.Code, p => p);
        db.Permissions.AddRange(permissions.Values);

        var supportRole = new Role { Name = "Support_Level_1", Description = "Read-only support" };
        var opsRole = new Role { Name = "Operations_Manager", Description = "Can unpause transactions" };
        var complianceRole = new Role { Name = "Compliance_Officer", Description = "Full operational + audit access" };
        db.Roles.AddRange(supportRole, opsRole, complianceRole);

        void Grant(Role role, params string[] codes)
        {
            foreach (var code in codes)
                db.RolePermissions.Add(new RolePermission { Role = role, Permission = permissions[code] });
        }

        Grant(supportRole, Permissions.TxRead);
        Grant(opsRole, Permissions.TxRead, Permissions.TxUnpause);
        Grant(complianceRole, Permissions.TxRead, Permissions.TxUnpause, Permissions.TxCancel, Permissions.AuditRead);

        var support = NewUser("support", "support@transfers.local", demoPassword);
        var ops = NewUser("ops", "ops@transfers.local", demoPassword);
        var compliance = NewUser("compliance", "compliance@transfers.local", demoPassword);
        db.Users.AddRange(support, ops, compliance);

        db.UserRoles.AddRange(
            new UserRole { User = support, Role = supportRole },
            new UserRole { User = ops, Role = opsRole },
            new UserRole { User = compliance, Role = complianceRole });

        await db.SaveChangesAsync();
    }

    private static User NewUser(string username, string email, string demoPassword) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = PasswordHasher.Hash(demoPassword),
        IsActive = true,
    };

    private static async Task SeedAuditAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var users = await db.Users.ToListAsync();
        var ops = users.FirstOrDefault(u => u.Username == "ops");
        var compliance = users.FirstOrDefault(u => u.Username == "compliance");

        db.AuditLogs.AddRange([
            new Domain.Audit.Entities.AuditLog
            {
                UserId = ops?.Id ?? Guid.NewGuid(),
                Username = "ops",
                ActionType = "tx:unpause",
                TargetTransactionId = "TX-1002",
                IpAddress = "192.168.1.10",
                Timestamp = now.AddMinutes(-45),
                Metadata = """{"source":"dashboard","note":"Manual unpause after partner retry"}""",
            },
            new Domain.Audit.Entities.AuditLog
            {
                UserId = compliance?.Id ?? Guid.NewGuid(),
                Username = "compliance",
                ActionType = "tx:unpause",
                TargetTransactionId = "TX-1001",
                IpAddress = "192.168.1.20",
                Timestamp = now.AddMinutes(-90),
                Metadata = """{"source":"dashboard"}""",
            },
        ]);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTransactionsAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var tx1 = new Transaction
        {
            InternalRef = "intref-tx-1001",
            TransactionId = "TX-1001",
            UserId = "user-77",
            RecipientName = "Alisher Karimov",
            Amount = 250.00m,
            Currency = "USD",
            Corridor = "RU->UZ",
            CurrentStatus = TransactionStatus.RegistrationSucceeded,
            IsPaused = false,
            CreatedAt = now.AddMinutes(-120),
            UpdatedAt = now.AddMinutes(-100),
        };
        AddHistory(tx1,
            (null, TransactionStatus.ConfirmPending, "Created", -120),
            (TransactionStatus.ConfirmPending, TransactionStatus.ConfirmSucceeded, "Sender confirmed", -118),
            (TransactionStatus.ConfirmSucceeded, TransactionStatus.CreditSucceeded, "Humo credit ok", -115),
            (TransactionStatus.CreditSucceeded, TransactionStatus.RegistrationSucceeded, "Partner ack", -100));
        tx1.CreditAttempts.Add(new CreditAttempt
        {
            AttemptNumber = 1, Gateway = CreditGateway.Humo, Status = OperationResult.Succeeded,
            GatewayResponse = "{\"code\":\"00\",\"message\":\"approved\"}", AttemptedAt = now.AddMinutes(-115),
        });
        tx1.PartnerRegistrations.Add(new PartnerRegistration
        {
            PartnerName = "GlobalRemit", Status = OperationResult.Succeeded,
            ReferenceId = "GR-55512", RegisteredAt = now.AddMinutes(-100),
        });

        var tx2 = new Transaction
        {
            InternalRef = "intref-tx-1002",
            TransactionId = "TX-1002",
            UserId = "user-77",
            RecipientName = "Dilnoza Yusupova",
            Amount = 540.50m,
            Currency = "USD",
            Corridor = "RU->UZ",
            CurrentStatus = TransactionStatus.Paused,
            IsPaused = true,
            CreatedAt = now.AddMinutes(-60),
            UpdatedAt = now.AddMinutes(-30),
        };
        AddHistory(tx2,
            (null, TransactionStatus.ConfirmPending, "Created", -60),
            (TransactionStatus.ConfirmPending, TransactionStatus.ConfirmSucceeded, "Sender confirmed", -58),
            (TransactionStatus.ConfirmSucceeded, TransactionStatus.CreditSucceeded, "Uzcard credit ok (2nd try)", -50),
            (TransactionStatus.CreditSucceeded, TransactionStatus.RegistrationFailedRetry, "Partner timeout", -40),
            (TransactionStatus.RegistrationFailedRetry, TransactionStatus.Paused, "Max retries reached - paused", -30));
        tx2.CreditAttempts.Add(new CreditAttempt
        {
            AttemptNumber = 1, Gateway = CreditGateway.Uzcard, Status = OperationResult.Failed,
            FailureCode = "51", GatewayResponse = "{\"code\":\"51\",\"message\":\"insufficient funds at gateway\"}",
            AttemptedAt = now.AddMinutes(-55),
        });
        tx2.CreditAttempts.Add(new CreditAttempt
        {
            AttemptNumber = 2, Gateway = CreditGateway.Uzcard, Status = OperationResult.Succeeded,
            GatewayResponse = "{\"code\":\"00\",\"message\":\"approved\"}", AttemptedAt = now.AddMinutes(-50),
        });
        tx2.PartnerRegistrations.Add(new PartnerRegistration
        {
            PartnerName = "GlobalRemit", Status = OperationResult.Failed,
            FailureReason = "Upstream timeout after 30s", RegisteredAt = now.AddMinutes(-40),
        });

        var tx3 = new Transaction
        {
            InternalRef = "intref-tx-1003",
            TransactionId = "TX-1003",
            UserId = "user-91",
            RecipientName = "Bekzod Tursunov",
            Amount = 75.00m,
            Currency = "USD",
            Corridor = "US->UZ",
            CurrentStatus = TransactionStatus.ConfirmSucceeded,
            IsPaused = false,
            CreatedAt = now.AddMinutes(-8),
            UpdatedAt = now.AddMinutes(-5),
        };
        AddHistory(tx3,
            (null, TransactionStatus.ConfirmPending, "Created", -8),
            (TransactionStatus.ConfirmPending, TransactionStatus.ConfirmSucceeded, "Sender confirmed", -6));

        db.Transactions.AddRange(tx1, tx2, tx3);
        await db.SaveChangesAsync();
    }

    private static void AddHistory(Transaction tx,
        params (TransactionStatus? from, TransactionStatus to, string reason, int minutesOffset)[] rows)
    {
        var now = DateTimeOffset.UtcNow;
        var i = 0;
        foreach (var (from, to, reason, offset) in rows)
        {
            tx.StatusHistory.Add(new TransactionStatusHistory
            {
                FromStatus = from,
                ToStatus = to,
                Reason = reason,
                OccurredAt = now.AddMinutes(offset),
                EventId = $"{tx.TransactionId}-seed-{i++}",
            });
        }
    }
}
