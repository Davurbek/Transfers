using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Transactions.Entities;

public class CreditAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public CreditGateway Gateway { get; set; }
    public OperationResult Status { get; set; }
    public string? FailureCode { get; set; }
    public string? GatewayResponse { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}
