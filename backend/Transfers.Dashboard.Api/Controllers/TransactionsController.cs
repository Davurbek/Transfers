using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.Api.Auth;
using Transfers.Dashboard.Api.Authorization;
using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Domain.Transactions;
using Transfers.Dashboard.Api.Dtos;
using Transfers.Dashboard.Api.Messaging;
using Transfers.Dashboard.Api.Services;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController(
    DashboardDbContext db,
    ICommandPublisher publisher,
    AuditService audit) : ControllerBase
{
    /// <summary>List/search transactions (read-only). Indexed on user/status/timestamp.</summary>
    [HttpGet]
    [RequirePermission(Permissions.TxRead)]
    public async Task<ActionResult<PagedResult<TransactionSummaryDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Transactions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t =>
                t.TransactionId.Contains(search) || t.RecipientName.Contains(search));

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransactionStatus>(status, true, out var parsed))
            query = query.Where(t => t.CurrentStatus == parsed);

        var total = await query.CountAsync(ct);
        var entities = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = entities.Select(ToSummary).ToList();
        return Ok(new PagedResult<TransactionSummaryDto>(items, page, pageSize, total));
    }

    /// <summary>Full audit trail for a single transaction (read-only).</summary>
    [HttpGet("{transactionId}")]
    [RequirePermission(Permissions.TxRead)]
    public async Task<ActionResult<TransactionDetailDto>> GetDetail(string transactionId, CancellationToken ct)
    {
        var tx = await db.Transactions.AsNoTracking()
            .Include(t => t.StatusHistory)
            .Include(t => t.CreditAttempts)
            .Include(t => t.PartnerRegistrations)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

        if (tx is null) return NotFound();

        var detail = new TransactionDetailDto(
            ToSummary(tx),
            tx.StatusHistory.OrderBy(h => h.OccurredAt)
                .Select(h => new StatusHistoryDto(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.Reason, h.OccurredAt))
                .ToList(),
            tx.CreditAttempts.OrderBy(c => c.AttemptNumber)
                .Select(c => new CreditAttemptDto(
                    c.AttemptNumber, c.Gateway.ToString(), c.Status.ToString(),
                    c.FailureCode, c.GatewayResponse, c.AttemptedAt))
                .ToList(),
            tx.PartnerRegistrations.OrderBy(p => p.RegisteredAt)
                .Select(p => new PartnerRegistrationDto(
                    p.PartnerName, p.Status.ToString(), p.FailureReason, p.ReferenceId, p.RegisteredAt))
                .ToList());

        return Ok(detail);
    }

    /// <summary>
    /// Unpause a stuck transaction. The dashboard does NOT mutate the transaction
    /// directly: it writes an audit entry and publishes a command to the Main App.
    /// Returns 202 Accepted; the new state arrives via the consumed event.
    /// </summary>
    [HttpPost("{transactionId}/unpause")]
    [RequirePermission(Permissions.TxUnpause)]
    [EnableRateLimiting("mutations")]
    public async Task<ActionResult<ActionAcceptedResponse>> Unpause(string transactionId, CancellationToken ct)
    {
        var tx = await db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);
        if (tx is null) return NotFound();

        if (!tx.IsPaused && tx.CurrentStatus != TransactionStatus.Paused)
            return Conflict(new { message = "Transaction is not in a paused state" });

        var command = new UnpauseTransactionCommand
        {
            TransactionId = transactionId,
            IssuedByUser = User.GetUsername(),
        };

        // 1) Immutable audit log entry for the write action.
        await audit.RecordAsync(
            User.GetUserId(), User.GetUsername(), Permissions.TxUnpause, transactionId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            metadata: $"{{\"commandId\":\"{command.CommandId}\"}}", ct);

        // 2) Publish command to the protected queue (Main App is source of truth).
        await publisher.PublishAsync(command, ct);

        return Accepted(new ActionAcceptedResponse(
            "Unpause command accepted; state will update once the Main App processes it.",
            transactionId, command.CommandId));
    }

    private static TransactionSummaryDto ToSummary(Transaction t) => new(
        t.TransactionId, t.UserId, t.RecipientName, t.Amount, t.Currency, t.Corridor,
        t.CurrentStatus.ToString(), t.IsPaused, t.CreatedAt, t.UpdatedAt);
}
