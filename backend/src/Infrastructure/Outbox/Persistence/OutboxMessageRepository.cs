using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Outbox.Entities;
using Universal.Transfers.Domain.Outbox.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Outbox.Persistence;

public sealed class OutboxMessageRepository(AppDbContext db) : IOutboxMessageRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default) =>
        await db.OutboxMessages.AddAsync(message, ct);

    public async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize, CancellationToken ct = default) =>
        await db.OutboxMessages
            .FromSqlRaw(@"
                SELECT * FROM outbox_messages
                WHERE ""PublishedAt"" IS NULL
                  AND ""DeadLetteredAt"" IS NULL
                  AND (""AvailableAt"" IS NULL OR ""AvailableAt"" <= NOW())
                ORDER BY ""Id""
                LIMIT {0}
                FOR UPDATE SKIP LOCKED", batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(long id, CancellationToken ct = default)
    {
        var msg = await db.OutboxMessages.FindAsync([id], ct);
        if (msg is not null)
            msg.MarkAsProcessed();
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        var msg = await db.OutboxMessages.FindAsync([id], ct);
        if (msg is not null)
            msg.MarkAsFailed(error);
    }

    public async Task IncrementRetryAsync(long id, CancellationToken ct = default)
    {
        var msg = await db.OutboxMessages.FindAsync([id], ct);
        if (msg is not null)
            msg.MarkAsFailed(msg.ErrorMessage ?? "Retry");
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
