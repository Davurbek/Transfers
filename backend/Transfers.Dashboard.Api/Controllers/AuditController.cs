using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.Api.Authorization;
using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Dtos;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController(DashboardDbContext db) : ControllerBase
{
    /// <summary>Read the immutable audit log (requires audit:read).</summary>
    [HttpGet]
    [RequirePermission(Permissions.AuditRead)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] string? transactionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(transactionId))
            query = query.Where(a => a.TargetTransactionId == transactionId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.Username, a.ActionType, a.TargetTransactionId,
                a.Timestamp, a.IpAddress, a.Metadata))
            .ToListAsync(ct);

        return Ok(new PagedResult<AuditLogDto>(items, page, pageSize, total));
    }
}
