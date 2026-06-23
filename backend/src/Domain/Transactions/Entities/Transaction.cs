using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Transactions.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InternalRef { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Corridor { get; set; } = string.Empty;
    public TransactionStatus CurrentStatus { get; set; } = TransactionStatus.ConfirmPending;
    public bool IsPaused { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CreditGateway CreditGateway { get; set; }
    public string RemitterPartner { get; set; } = string.Empty;

    public ICollection<TransactionStatusHistory> StatusHistory { get; set; } = new List<TransactionStatusHistory>();
    public ICollection<CreditAttempt> CreditAttempts { get; set; } = new List<CreditAttempt>();
    public ICollection<PartnerRegistration> PartnerRegistrations { get; set; } = new List<PartnerRegistration>();
}
