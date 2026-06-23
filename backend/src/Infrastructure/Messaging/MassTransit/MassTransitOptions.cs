namespace Universal.Transfers.Infrastructure.Messaging.MassTransit;

public sealed class MassTransitOptions
{
    public const string SectionName = "MassTransit";

    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = "transfers-dashboard";

    public string? SecurityProtocol { get; init; }
    public string? SaslMechanism { get; init; }
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }
    public string? SslCaLocation { get; init; }
    public bool EnableSslCertificateVerification { get; init; } = true;

    public int PrefetchCount { get; init; } = 10;
    public int ConcurrentMessageLimit { get; init; } = 5;

    public RetryPolicy Retry { get; init; } = new();
    public Dictionary<string, string> Topics { get; init; } = [];

    public sealed class RetryPolicy
    {
        public int Limit { get; init; } = 5;
        public int MinIntervalSeconds { get; init; } = 5;
        public int MaxIntervalSeconds { get; init; } = 60;
        public int IntervalDeltaSeconds { get; init; } = 5;
    }
}
