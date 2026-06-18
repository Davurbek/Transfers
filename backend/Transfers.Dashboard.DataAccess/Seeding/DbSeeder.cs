using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.DataAccess.Context;
using Transfers.Dashboard.Domain.Authorization;
using Transfers.Dashboard.Domain.Entities.Auth;
using Transfers.Dashboard.Domain.Entities.Transactions;
using Transfers.Dashboard.Domain.Security;

namespace Transfers.Dashboard.DataAccess.Seeding;

/// <summary>Creates the database and seeds demo auth + transaction data.</summary>
public static class DbSeeder
{
    public const string DemoPassword = "Passw0rd!";

    public static async Task SeedAsync(DashboardDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Permissions.AnyAsync())
            await SeedAuthAsync(db);

        if (!await db.Transactions.AnyAsync())
            await SeedTransactionsAsync(db);
    }

    private static async Task SeedAuthAsync(DashboardDbContext db)
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

        var support = NewUser("support", "support@transfers.local");
        var ops = NewUser("ops", "ops@transfers.local");
        var compliance = NewUser("compliance", "compliance@transfers.local");
        db.Users.AddRange(support, ops, compliance);

        db.UserRoles.AddRange(
            new UserRole { User = support, Role = supportRole },
            new UserRole { User = ops, Role = opsRole },
            new UserRole { User = compliance, Role = complianceRole });

        await db.SaveChangesAsync();
    }

    private static User NewUser(string username, string email) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = PasswordHasher.Hash(DemoPassword),
        IsActive = true,
    };

    private static async Task SeedTransactionsAsync(DashboardDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var tx1 = new Transaction
        {
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
            (TransactionStatus.ConfirmSucceeded, TransactionStatus.CreditPending, "Crediting recipient", -117),
            (TransactionStatus.CreditPending, TransactionStatus.CreditSucceeded, "Humo credit ok", -115),
            (TransactionStatus.CreditSucceeded, TransactionStatus.RegistrationPending, "Registering with partner", -112),
            (TransactionStatus.RegistrationPending, TransactionStatus.RegistrationSucceeded, "Partner ack", -100));
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
            (TransactionStatus.ConfirmSucceeded, TransactionStatus.CreditPending, "Crediting recipient", -57),
            (TransactionStatus.CreditPending, TransactionStatus.CreditSucceeded, "Uzcard credit ok (2nd try)", -50),
            (TransactionStatus.CreditSucceeded, TransactionStatus.RegistrationPending, "Registering with partner", -48),
            (TransactionStatus.RegistrationPending, TransactionStatus.RegistrationFailedRetry, "Partner timeout", -40),
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
            TransactionId = "TX-1003",
            UserId = "user-91",
            RecipientName = "Bekzod Tursunov",
            Amount = 75.00m,
            Currency = "USD",
            Corridor = "US->UZ",
            CurrentStatus = TransactionStatus.CreditPending,
            IsPaused = false,
            CreatedAt = now.AddMinutes(-8),
            UpdatedAt = now.AddMinutes(-5),
        };
        AddHistory(tx3,
            (null, TransactionStatus.ConfirmPending, "Created", -8),
            (TransactionStatus.ConfirmPending, TransactionStatus.ConfirmSucceeded, "Sender confirmed", -6),
            (TransactionStatus.ConfirmSucceeded, TransactionStatus.CreditPending, "Crediting recipient", -5));

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
