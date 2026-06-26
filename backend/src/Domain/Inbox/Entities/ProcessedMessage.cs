namespace Universal.Transfers.Domain.Inbox.Entities;

public sealed class ProcessedMessage
{
    public long Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }

    public static ProcessedMessage Create(string idempotencyKey, string eventType, DateTime processedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (idempotencyKey.Length > 200)
            throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(idempotencyKey));

        return new ProcessedMessage
        {
            IdempotencyKey = idempotencyKey,
            EventType = eventType,
            ProcessedAt = processedAt
        };
    }
}
