using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Inbox.Entities;
using Universal.Transfers.Domain.Inbox.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Inbox.Persistence;

public sealed class ProcessedMessageRepository(AppDbContext db) : IProcessedMessageRepository
{
    public Task<ProcessedMessage?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        db.ProcessedMessages.FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey, ct);

    public void Add(ProcessedMessage message) =>
        db.ProcessedMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
