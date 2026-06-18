using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Transfers.Dashboard.Api.Authorization;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Business.Services;
using Transfers.Dashboard.Domain.Authorization;
using Transfers.Dashboard.Domain.Common;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController(IAuditService auditService) : ControllerBase
{
    /// <summary>
    /// Filtered + paginated immutable audit log (requires audit:read).
    /// Query params: targetTransactionId, actionType, username, fromDate, toDate, page, pageSize.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] AuditFilter filter, CancellationToken ct)
    {
        var result = await auditService.SearchAsync(filter, ct);
        return Ok(result);
    }
}
