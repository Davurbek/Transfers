using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Transactions.Persistence;

public sealed class TransactionRepository(AppDbContext db) : ITransactionRepository
{
    public async Task<PagedResult<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var query = db.Transactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(t => t.TransactionId.Contains(term) || t.RecipientName.Contains(term));
        }

        if (filter.Status is { } status)
            query = query.Where(t => t.CurrentStatus == status);

        if (!string.IsNullOrWhiteSpace(filter.UserId))
            query = query.Where(t => t.UserId == filter.UserId);

        if (filter.IsPaused is { } paused)
            query = query.Where(t => t.IsPaused == paused);

        if (filter.FromDate is { } from)
            query = query.Where(t => t.CreatedAt >= from);

        if (filter.ToDate is { } to)
            query = query.Where(t => t.CreatedAt <= to);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Transaction>(items, filter.Page, filter.PageSize, total);
    }

    public Task<Transaction?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default) =>
        db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

    public Task<Transaction?> GetDetailAsync(string transactionId, CancellationToken ct = default) =>
        db.Transactions.AsNoTracking()
            .Include(t => t.StatusHistory)
            .Include(t => t.CreditAttempts)
            .Include(t => t.PartnerRegistrations)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default) =>
        await db.Transactions.AddAsync(transaction, ct);

    public void AddStatusHistory(TransactionStatusHistory history) =>
        db.TransactionStatusHistory.Add(history);

    public Task<bool> StatusEventExistsAsync(string eventId, CancellationToken ct = default) =>
        db.TransactionStatusHistory.AnyAsync(h => h.EventId == eventId, ct);
}
