using Microsoft.Extensions.Logging;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Business.Mapping;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.Domain.Common;
using Transfers.Dashboard.Domain.Entities.Audit;

namespace Transfers.Dashboard.Business.Services;

public sealed class AuditService(
    IAuditRepository auditRepository,
    IUnitOfWork unitOfWork,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditFilter filter, CancellationToken ct = default)
    {
        var page = await auditRepository.SearchAsync(filter, ct);
        return page.Map(a => a.ToDto());
    }

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        await auditRepository.AddAsync(new AuditLog
        {
            UserId = entry.UserId,
            Username = entry.Username,
            ActionType = entry.ActionType,
            TargetTransactionId = entry.TargetTransactionId,
            IpAddress = entry.IpAddress,
            Metadata = entry.Metadata,
            Timestamp = DateTimeOffset.UtcNow,
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("AUDIT {Action} by {User} on {Target} from {Ip}",
            entry.ActionType, entry.Username, entry.TargetTransactionId, entry.IpAddress);
    }
}
