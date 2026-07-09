using Universal.Transfers.Domain.Inbox.Entities;

namespace Universal.Transfers.Domain.Inbox.Interfaces;

public interface IInboxEventRepository
{
    Task AddAsync(InboxEvent inboxEvent, CancellationToken ct = default);
    Task<List<InboxEvent>> GetUnprocessedEventsAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(long id, CancellationToken ct = default);
    Task MarkFailedAsync(long id, string error, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
