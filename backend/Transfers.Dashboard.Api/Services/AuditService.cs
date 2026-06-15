using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Domain.Audit;

namespace Transfers.Dashboard.Api.Services;

/// <summary>Writes immutable audit entries for privileged write actions.</summary>
public class AuditService(DashboardDbContext db, ILogger<AuditService> logger)
{
    public async Task RecordAsync(
        Guid userId,
        string username,
        string actionType,
        string? targetTransactionId,
        string ipAddress,
        string? metadata = null,
        CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Username = username,
            ActionType = actionType,
            TargetTransactionId = targetTransactionId,
            IpAddress = ipAddress,
            Metadata = metadata,
            Timestamp = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AUDIT {Action} by {User} on {Target} from {Ip}",
            actionType, username, targetTransactionId, ipAddress);
    }
}
