namespace Universal.Transfers.Domain.DeadLetter.Entities;

public sealed class DeadLetterMessage
{
    public long Id { get; set; }
    public string OriginalTopic { get; set; } = string.Empty;
    public long OriginalOffset { get; set; }
    public int OriginalPartition { get; set; }
    public string MessageKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 5;
    public DateTime? LastRetryAt { get; set; }
    public string? LastRetryError { get; set; }
    public bool Replayed { get; set; }
    public DateTime? ReplayedAt { get; set; }
}
