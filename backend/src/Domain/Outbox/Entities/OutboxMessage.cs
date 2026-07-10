using System.Text.Json;

namespace Universal.Transfers.Domain.Outbox.Entities;

public sealed class OutboxMessage
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Headers { get; private set; }
    public DateTime? AvailableAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public void SetHeader(string key, string? value)
    {
        var dict = string.IsNullOrWhiteSpace(Headers)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(Headers) ?? new Dictionary<string, string>();

        if (value is null)
            dict.Remove(key);
        else
            dict[key] = value;

        Headers = JsonSerializer.Serialize(dict);
    }

    public string? GetHeader(string key)
    {
        if (string.IsNullOrWhiteSpace(Headers))
            return null;

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(Headers);
        return dict is not null && dict.TryGetValue(key, out var val) ? val : null;
    }

    public void Defer(DateTime availableAt) => AvailableAt = availableAt;

    public void MarkAsProcessed() => PublishedAt = DateTime.UtcNow;

    public void MarkAsFailed(string error)
    {
        ErrorMessage = error;
        RetryCount++;
    }

    public void MarkAsDeadLettered(string error)
    {
        DeadLetteredAt = DateTime.UtcNow;
        ErrorMessage = error;
    }
}
