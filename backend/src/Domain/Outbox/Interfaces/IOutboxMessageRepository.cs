using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Domain.Outbox.Interfaces;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(long id, CancellationToken ct = default);
    Task MarkFailedAsync(long id, string error, CancellationToken ct = default);
    Task IncrementRetryAsync(long id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
