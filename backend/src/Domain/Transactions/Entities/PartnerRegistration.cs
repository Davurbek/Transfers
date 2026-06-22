using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Transactions.Entities;

public class PartnerRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public string PartnerName { get; set; } = string.Empty;
    public OperationResult Status { get; set; }
    public string? FailureReason { get; set; }
    public string? ReferenceId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}
