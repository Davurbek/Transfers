using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.DeadLetter.Entities;
using Universal.Transfers.Domain.DeadLetter.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.DeadLetter.Persistence;

public sealed class DeadLetterRepository(AppDbContext db) : IDeadLetterRepository
{
    public async Task AddAsync(DeadLetterMessage message, CancellationToken ct = default) =>
        await db.DeadLetterMessages.AddAsync(message, ct);

    public async Task<List<DeadLetterMessage>> GetUnreplayedMessagesAsync(int batchSize, CancellationToken ct = default) =>
        await db.DeadLetterMessages
            .Where(m => !m.Replayed && m.RetryCount < m.MaxRetries)
            .OrderBy(m => m.FailedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<DeadLetterMessage?> GetByIdAsync(long id, CancellationToken ct = default) =>
        await db.DeadLetterMessages.FindAsync([id], ct);

    public async Task MarkReplayedAsync(long id, CancellationToken ct = default)
    {
        var msg = await db.DeadLetterMessages.FindAsync([id], ct);
        if (msg is not null)
        {
            msg.Replayed = true;
            msg.ReplayedAt = DateTime.UtcNow;
        }
    }

    public async Task IncrementRetryAsync(long id, string? error = null, CancellationToken ct = default)
    {
        var msg = await db.DeadLetterMessages.FindAsync([id], ct);
        if (msg is not null)
        {
            msg.RetryCount++;
            msg.LastRetryAt = DateTime.UtcNow;
            msg.LastRetryError = error;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
