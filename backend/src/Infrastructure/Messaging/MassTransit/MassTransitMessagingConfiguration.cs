using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Universal.Transfers.Infrastructure.Messaging.MassTransit;
using Universal.Transfers.Infrastructure.Messaging.MassTransit.Consumers;
using Universal.Transfers.Infrastructure.Messaging.MassTransit.Filters;
using ApiV2 = Universal.Transfers.Infrastructure.Messaging.MassTransit.EventRouter.ApiV2;

namespace Microsoft.Extensions.DependencyInjection;

public static class MassTransitMessagingConfiguration
{
    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        [typeof(ApiV2.TransactionInitiatedEvent).FullName!] = typeof(ApiV2.TransactionInitiatedEvent),
        [typeof(ApiV2.TransactionCreditCompletedEvent).FullName!] = typeof(ApiV2.TransactionCreditCompletedEvent),
        [typeof(ApiV2.TransactionCreditFailedEvent).FullName!] = typeof(ApiV2.TransactionCreditFailedEvent),
        [typeof(ApiV2.TransactionCreditFailedRetryEvent).FullName!] = typeof(ApiV2.TransactionCreditFailedRetryEvent),
        [typeof(ApiV2.TransactionCreditRetryRequestedEvent).FullName!] = typeof(ApiV2.TransactionCreditRetryRequestedEvent),
        [typeof(ApiV2.TransactionRegistrationCompletedEvent).FullName!] = typeof(ApiV2.TransactionRegistrationCompletedEvent),
        [typeof(ApiV2.TransactionRegistrationFailedRetryEvent).FullName!] = typeof(ApiV2.TransactionRegistrationFailedRetryEvent),
        [typeof(ApiV2.TransactionRegistrationRetryRequestedEvent).FullName!] = typeof(ApiV2.TransactionRegistrationRetryRequestedEvent),
        [typeof(ApiV2.TransactionPausedEvent).FullName!] = typeof(ApiV2.TransactionPausedEvent),
        [typeof(ApiV2.TransactionUnpausedEvent).FullName!] = typeof(ApiV2.TransactionUnpausedEvent),
    };

    public static IServiceCollection AddMassTransitMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(MassTransitOptions.SectionName)
            .Get<MassTransitOptions>()
            ?? throw new InvalidOperationException($"'{MassTransitOptions.SectionName}' section is missing.");

        services.AddMassTransit(bus =>
        {
            bus.DisableUsageTelemetry();

            bus.AddRider(rider =>
            {
                rider.AddConsumer<MainEventConsumer>();

                rider.UsingKafka((context, k) =>
                {
                    k.Host(options.BootstrapServers, h =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.SecurityProtocol))
                            ApplySecurity(h, options);
                    });

                    foreach (var kvp in options.Topics)
                    {
                        var topic = kvp.Value;
                        if (string.IsNullOrWhiteSpace(topic)) continue;

                        if (!EventTypeMap.TryGetValue(kvp.Key, out var eventType))
                            throw new InvalidOperationException($"Unknown event type key '{kvp.Key}'. Add it to EventTypeMap.");

                        var method = typeof(MassTransitMessagingConfiguration)
                            .GetMethod(nameof(SubscribeCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                            .MakeGenericMethod(eventType);

                        method.Invoke(null, [k, context, options, topic]);
                    }
                });
            });
        });

        return services;
    }

    private static void SubscribeCore<TEvent>(
        IKafkaFactoryConfigurator k,
        IRiderRegistrationContext context,
        MassTransitOptions options,
        string topic)
        where TEvent : class
    {
        k.TopicEndpoint<Ignore, TEvent>(topic, options.GroupId, e =>
        {
            e.AutoOffsetReset = AutoOffsetReset.Earliest;
            e.PrefetchCount = options.PrefetchCount;
            e.ConcurrentMessageLimit = options.ConcurrentMessageLimit;

            e.UseRawJsonDeserializer(
                RawSerializerOptions.AnyMessageType | RawSerializerOptions.AddTransportHeaders);

            e.UseMessageRetry(r =>
            {
                r.Exponential(
                    retryLimit: options.Retry.Limit,
                    minInterval: TimeSpan.FromSeconds(options.Retry.MinIntervalSeconds),
                    maxInterval: TimeSpan.FromSeconds(options.Retry.MaxIntervalSeconds),
                    intervalDelta: TimeSpan.FromSeconds(options.Retry.IntervalDeltaSeconds));
            });

            e.UseConsumeFilter(typeof(IdempotencyFilter<>), context);
            e.ConfigureConsumer<MainEventConsumer>(context);
        });
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
