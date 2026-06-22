using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Transactions.Interfaces;
using Universal.Transfers.Domain.Audit.Interfaces;
using Universal.Transfers.Domain.Audit.Entities;
using Universal.Transfers.Application.Transactions.DTOs;
using Universal.Transfers.Application.Transactions.Mappings;
using Universal.Transfers.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace Universal.Transfers.Application.Transactions;

public class TransactionService(
    ITransactionRepository txRepo,
    IAuditRepository auditRepo,
    ICommandPublisher commandPublisher,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<PagedResult<TransactionListItemDto>> SearchAsync(
        string? search, string? status, string? userId, bool? isPaused,
        DateTimeOffset? fromDate, DateTimeOffset? toDate,
        int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new TransactionFilter
        {
            Search = search,
            UserId = userId,
            IsPaused = isPaused,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize,
        };

        if (Enum.TryParse<Domain.Transactions.Enums.TransactionStatus>(status, ignoreCase: true, out var parsed))
            filter.Status = parsed;

        var paged = await txRepo.SearchAsync(filter, ct);
        return paged.Map(TransactionMappings.ToListItemDto);
    }

    public async Task<TransactionDetailDto?> GetDetailAsync(string transactionId, CancellationToken ct = default)
    {
        var tx = await txRepo.GetDetailAsync(transactionId, ct);
        return tx is null ? null : TransactionMappings.ToDetailDto(tx);
    }

    public async Task<UnpauseResult> UnpauseAsync(string transactionId, Guid userId, string username, string ipAddress, CancellationToken ct = default)
    {
        var tx = await txRepo.GetByTransactionIdAsync(transactionId, ct);
        if (tx is null)
            return new UnpauseResult(UnpauseOutcome.NotFound, null);

        if (!tx.IsPaused && tx.CurrentStatus != Domain.Transactions.Enums.TransactionStatus.Paused)
            return new UnpauseResult(UnpauseOutcome.NotPaused, null);

        var command = new UnpauseTransactionCommand(transactionId, username);
        await commandPublisher.PublishAsync(command, ct);

        var audit = new AuditLog
        {
            UserId = userId,
            Username = username,
            ActionType = "tx:unpause",
            TargetTransactionId = transactionId,
            IpAddress = ipAddress,
            Metadata = """{"source":"dashboard"}""",
        };
        await auditRepo.AddAsync(audit, ct);

        logger.LogInformation("Unpause command published for {TxId} by {User}", transactionId, username);
        return new UnpauseResult(UnpauseOutcome.Accepted, command.CommandId);
    }
}
