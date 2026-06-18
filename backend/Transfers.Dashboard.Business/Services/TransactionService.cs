using Microsoft.Extensions.Logging;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Business.Mapping;
using Transfers.Dashboard.Business.Messaging;
using Transfers.Dashboard.Domain.Authorization;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.Domain.Common;
using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Business.Services;

public sealed class TransactionService(
    ITransactionRepository transactions,
    IAuditService auditService,
    ICommandPublisher commandPublisher,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<PagedResult<TransactionSummaryDto>> SearchAsync(
        TransactionFilter filter, CancellationToken ct = default)
    {
        var page = await transactions.SearchAsync(filter, ct);
        return page.Map(t => t.ToSummary());
    }

    public async Task<TransactionDetailDto?> GetDetailAsync(string transactionId, CancellationToken ct = default)
    {
        var tx = await transactions.GetDetailAsync(transactionId, ct);
        return tx?.ToDetail();
    }

    public async Task<UnpauseResult> RequestUnpauseAsync(UnpauseRequest request, CancellationToken ct = default)
    {
        var tx = await transactions.GetByTransactionIdAsync(request.TransactionId, ct);
        if (tx is null)
            return new UnpauseResult(UnpauseOutcome.NotFound, null);

        if (!tx.IsPaused && tx.CurrentStatus != TransactionStatus.Paused)
            return new UnpauseResult(UnpauseOutcome.NotPaused, null);

        var command = new UnpauseTransactionCommand
        {
            TransactionId = request.TransactionId,
            IssuedByUser = request.Username,
        };

        // 1) Immutable audit entry for the write action.
        await auditService.RecordAsync(new AuditEntry(
            request.UserId, request.Username, Permissions.TxUnpause, request.TransactionId,
            request.IpAddress, $"{{\"commandId\":\"{command.CommandId}\"}}"), ct);

        // 2) Publish command to the protected queue (Main App is the source of truth).
        await commandPublisher.PublishAsync(command, ct);

        logger.LogInformation("Unpause requested for {TxId} by {User}", request.TransactionId, request.Username);
        return new UnpauseResult(UnpauseOutcome.Accepted, command.CommandId);
    }
}
