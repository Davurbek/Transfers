namespace Universal.Transfers.Domain.Transactions.Enums;

public enum TransactionStatus
{
    ConfirmPending,
    ConfirmSucceeded,
    ConfirmFailed,
    CreditPending,
    CreditSucceeded,
    CreditFailed,
    RegistrationPending,
    RegistrationFailedRetry,
    RegistrationSucceeded,
    Paused,
    Cancelled
}
