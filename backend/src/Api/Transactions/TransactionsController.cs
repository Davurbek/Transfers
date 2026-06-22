using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Universal.Transfers.Api.Auth;
using Universal.Transfers.Application.Transactions;
using Universal.Transfers.Application.Transactions.DTOs;

namespace Universal.Transfers.Api.Transactions;

[Authorize]
[ApiController]
[Route("transactions")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    [Authorize(Policy = "permission:tx:read")]
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? userId,
        [FromQuery] bool? isPaused,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await transactionService.SearchAsync(search, status, userId, isPaused, fromDate, toDate, page, pageSize, ct);
        return Ok(result);
    }

    [Authorize(Policy = "permission:tx:read")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(string id, CancellationToken ct)
    {
        var tx = await transactionService.GetDetailAsync(id, ct);
        if (tx is null) return NotFound();
        return Ok(tx);
    }

    [Authorize(Policy = "permission:tx:unpause")]
    [HttpPost("{id}/unpause")]
    public async Task<IActionResult> Unpause(string id, CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var username = HttpContext.User.Identity?.Name ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

        var result = await transactionService.UnpauseAsync(id, userId, username, ip, ct);

        return result.Outcome switch
        {
            UnpauseOutcome.Accepted => Accepted(new { commandId = result.CommandId }),
            UnpauseOutcome.NotFound => NotFound(new { message = "Transaction not found" }),
            UnpauseOutcome.NotPaused => BadRequest(new { message = "Transaction is not paused" }),
            _ => StatusCode(500),
        };
    }
}
