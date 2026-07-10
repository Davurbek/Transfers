using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Universal.Transfers.Domain.Common.Entities;
using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Infrastructure.Common.Messaging.Eventing;

public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null)
            return await base.SavingChangesAsync(eventData, result, ct);

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entry in aggregates)
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    EventType = domainEvent.GetType().FullName!,
                    AggregateId = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                    CreatedAt = DateTime.UtcNow,
                };

                context.Add(outboxMessage);
            }

            entry.Entity.ClearDomainEvents();
        }

        return await base.SavingChangesAsync(eventData, result, ct);
    }
}
