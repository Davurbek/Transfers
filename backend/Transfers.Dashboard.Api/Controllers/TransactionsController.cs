using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Transfers.Dashboard.Api.Auth;
using Transfers.Dashboard.Api.Authorization;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Business.Services;
using Transfers.Dashboard.Domain.Authorization;
using Transfers.Dashboard.Domain.Common;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    /// <summary>
    /// Filtered + paginated transaction list for the UI.
    /// Query params: search, status, userId, isPaused, fromDate, toDate, page, pageSize.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.TxRead)]
    [ProducesResponseType(typeof(PagedResult<TransactionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TransactionSummaryDto>>> List(
        [FromQuery] TransactionFilter filter, CancellationToken ct)
    {
        var result = await transactionService.SearchAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Full audit trail for a single transaction (read-only).</summary>
    [HttpGet("{transactionId}")]
    [RequirePermission(Permissions.TxRead)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDetailDto>> GetDetail(string transactionId, CancellationToken ct)
    {
        var detail = await transactionService.GetDetailAsync(transactionId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Unpause a stuck transaction. The dashboard does NOT mutate the transaction
    /// directly: the service writes an audit entry and publishes a command to the
    /// Main App. Returns 202 Accepted; the new state arrives via the consumed event.
    /// </summary>
    [HttpPost("{transactionId}/unpause")]
    [RequirePermission(Permissions.TxUnpause)]
    [EnableRateLimiting("mutations")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpause(string transactionId, CancellationToken ct)
    {
        var request = new UnpauseRequest(
            transactionId,
            User.GetUserId(),
            User.GetUsername(),
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var result = await transactionService.RequestUnpauseAsync(request, ct);

        return result.Outcome switch
        {
            UnpauseOutcome.NotFound => NotFound(),
            UnpauseOutcome.NotPaused => Conflict(new { message = "Transaction is not in a paused state" }),
            _ => Accepted(new
            {
                message = "Unpause command accepted; state will update once the Main App processes it.",
                transactionId,
                commandId = result.CommandId,
            }),
        };
    }
}
