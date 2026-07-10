using Universal.Transfers.Domain.DeadLetter.Entities;

namespace Universal.Transfers.Domain.DeadLetter.Interfaces;

public interface IDeadLetterRepository
{
    Task AddAsync(DeadLetterMessage message, CancellationToken ct = default);
    Task<List<DeadLetterMessage>> GetUnreplayedMessagesAsync(int batchSize, CancellationToken ct = default);
    Task<DeadLetterMessage?> GetByIdAsync(long id, CancellationToken ct = default);
    Task MarkReplayedAsync(long id, CancellationToken ct = default);
    Task IncrementRetryAsync(long id, string? error = null, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
