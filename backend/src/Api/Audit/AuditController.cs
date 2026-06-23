using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Universal.Transfers.Application.Audit;

namespace Universal.Transfers.Api.Audit;

[Authorize]
[ApiController]
[Route("audit")]
public class AuditController(IAuditService auditService) : ControllerBase
{
    [Authorize(Policy = "permission:audit:read")]
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? targetTransactionId,
        [FromQuery] string? actionType,
        [FromQuery] string? username,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct = default)
    {
        var result = await auditService.SearchAsync(targetTransactionId, actionType, username, fromDate, toDate, page, pageSize, ct);
        return Ok(result);
    }
}
