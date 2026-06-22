using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Transactions.Entities;

public class TransactionStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public TransactionStatus? FromStatus { get; set; }
    public TransactionStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? EventId { get; set; }
}
