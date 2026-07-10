using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Infrastructure.Messaging.Kafka;
using Universal.Transfers.Infrastructure.Messaging.MassTransit;
using Universal.Transfers.Infrastructure.Messaging.MassTransit.Consumers;
using Universal.Transfers.Infrastructure.Messaging.MassTransit.Filters;

namespace Microsoft.Extensions.DependencyInjection;

public static class MassTransitMessagingConfiguration
{
    public static IServiceCollection AddMassTransitMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var mtOptions = configuration
            .GetSection(MassTransitOptions.SectionName)
            .Get<MassTransitOptions>()
            ?? throw new InvalidOperationException($"'{MassTransitOptions.SectionName}' section is missing.");

        var kafkaOptions = configuration
            .GetSection(KafkaOptions.SectionName)
            .Get<KafkaOptions>()
            ?? throw new InvalidOperationException($"'{KafkaOptions.SectionName}' section is missing.");

        services.AddMassTransit(bus =>
        {
            bus.DisableUsageTelemetry();
            bus.UsingInMemory();

            bus.AddRider(rider =>
            {
                rider.AddConsumer<MainEventConsumer>();

                rider.UsingKafka((context, k) =>
                {
                    k.Host(mtOptions.BootstrapServers, h =>
                    {
                        if (!string.IsNullOrWhiteSpace(mtOptions.SecurityProtocol))
                            ApplySecurity(h, mtOptions);
                    });

                    var topics = kafkaOptions.Topics.Values
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct()
                        .ToList();

                    foreach (var topic in topics)
                    {
                        k.TopicEndpoint<Ignore, string>(topic, mtOptions.GroupId, e =>
                        {
                            e.AutoOffsetReset = AutoOffsetReset.Earliest;
                            e.PrefetchCount = mtOptions.PrefetchCount;
                            e.ConcurrentMessageLimit = mtOptions.ConcurrentMessageLimit;

                            e.UseMessageRetry(r =>
                            {
                                r.Exponential(
                                    retryLimit: mtOptions.Retry.Limit,
                                    minInterval: TimeSpan.FromSeconds(mtOptions.Retry.MinIntervalSeconds),
                                    maxInterval: TimeSpan.FromSeconds(mtOptions.Retry.MaxIntervalSeconds),
                                    intervalDelta: TimeSpan.FromSeconds(mtOptions.Retry.IntervalDeltaSeconds));
                            });

                            e.ConfigureConsumer<MainEventConsumer>(context);
                        });
                    }
                });
            });
        });

        return services;
    }

    private static void ApplySecurity(IKafkaHostConfigurator h, MassTransitOptions options)
    {
        if (!Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, ignoreCase: true, out var protocol))
            return;

        if (protocol is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl)
        {
            if (!Enum.TryParse<SaslMechanism>(options.SaslMechanism, ignoreCase: true, out var mechanism))
                mechanism = SaslMechanism.Plain;

            h.UseSasl(s =>
            {
                s.SecurityProtocol = protocol;
                s.Mechanism = mechanism;
                s.Username = options.SaslUsername;
                s.Password = options.SaslPassword;
            });
        }

        if (protocol is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl)
        {
            h.UseSsl(s =>
            {
                if (!string.IsNullOrWhiteSpace(options.SslCaLocation))
                    s.CaLocation = options.SslCaLocation;
                s.EnableCertificateVerification = options.EnableSslCertificateVerification;
            });
        }
    }
}
