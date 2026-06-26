using MassTransit;
using Microsoft.Extensions.Logging;
using Universal.Transfers.Domain.Inbox.Interfaces;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit.Filters;

public sealed class IdempotencyFilter<TMessage>(
    IProcessedMessageRepository repo,
    ILogger<IdempotencyFilter<TMessage>> logger) : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private const string IdempotencyHeader = "idempotency-key";

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        var ct = context.CancellationToken;
        var key = ReadIdempotencyKey(context);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["IdempotencyKey"] = key,
            ["MessageType"] = typeof(TMessage).Name,
        });

        var seen = await repo.GetByIdempotencyKeyAsync(key, ct);
        if (seen is not null)
        {
            logger.LogWarning("Skipping duplicate message. ProcessedAt={ProcessedAt}", seen.ProcessedAt);
            return;
        }

        await next.Send(context);

        repo.Add(Domain.Inbox.Entities.ProcessedMessage.Create(key, typeof(TMessage).FullName!, DateTime.UtcNow));
        await repo.SaveChangesAsync(ct);
    }

    public void Probe(ProbeContext context) =>
        context.CreateFilterScope("idempotency");

    private static string ReadIdempotencyKey(ConsumeContext context)
    {
        var key = context.Headers.Get<string>(IdempotencyHeader);
        if (!string.IsNullOrWhiteSpace(key))
            return key;
        return context.MessageId?.ToString("N") ?? Guid.NewGuid().ToString("N");
    }
}
