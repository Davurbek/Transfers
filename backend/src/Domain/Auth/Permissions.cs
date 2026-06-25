namespace Universal.Transfers.Domain.Auth;

public static class Permissions
{
    public const string TxRead = "tx:read";
    public const string TxUnpause = "tx:unpause";
    public const string TxCancel = "tx:cancel";
    public const string AuditRead = "audit:read";
    public const string Admin = "admin";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [TxRead] = "View transactions and their full history",
        [TxUnpause] = "Release a paused transaction (write action)",
        [TxCancel] = "Cancel a transaction (write action)",
        [AuditRead] = "Read the audit log",
        [Admin] = "Manage users, roles, and permissions",
    };
}
