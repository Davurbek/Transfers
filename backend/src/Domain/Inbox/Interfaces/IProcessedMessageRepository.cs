using Universal.Transfers.Domain.Inbox.Entities;

namespace Universal.Transfers.Domain.Inbox.Interfaces;

public interface IProcessedMessageRepository
{
    Task<ProcessedMessage?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    void Add(ProcessedMessage message);
    Task SaveChangesAsync(CancellationToken ct = default);
}
