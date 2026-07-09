using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Inbox.Entities;
using Universal.Transfers.Domain.Inbox.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Inbox.Persistence;

public sealed class InboxEventRepository(AppDbContext db) : IInboxEventRepository
{
    public async Task AddAsync(InboxEvent inboxEvent, CancellationToken ct = default) =>
        await db.InboxEvents.AddAsync(inboxEvent, ct);

    public async Task<List<InboxEvent>> GetUnprocessedEventsAsync(int batchSize, CancellationToken ct = default) =>
        await db.InboxEvents
            .Where(e => !e.Processed)
            .OrderBy(e => e.OccurredOn)
            .ThenBy(e => e.Id)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(long id, CancellationToken ct = default)
    {
        var ev = await db.InboxEvents.FindAsync([id], ct);
        if (ev is not null)
        {
            ev.Processed = true;
            ev.ProcessedAt = DateTime.UtcNow;
        }
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        var ev = await db.InboxEvents.FindAsync([id], ct);
        if (ev is not null)
        {
            ev.ErrorMessage = error;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
