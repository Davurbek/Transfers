using MassTransit;
using Microsoft.Extensions.Logging;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;
using ApiV2 = Universal.Transfers.Infrastructure.Messaging.MassTransit.EventRouter.ApiV2;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit.Consumers;

public sealed class MainEventConsumer(
    IEventProjector projector,
    ILogger<MainEventConsumer> logger) :
    IConsumer<ApiV2.TransactionInitiatedEvent>,
    IConsumer<ApiV2.TransactionCreditCompletedEvent>,
    IConsumer<ApiV2.TransactionCreditFailedEvent>,
    IConsumer<ApiV2.TransactionCreditFailedRetryEvent>,
    IConsumer<ApiV2.TransactionCreditRetryRequestedEvent>,
    IConsumer<ApiV2.TransactionRegistrationCompletedEvent>,
    IConsumer<ApiV2.TransactionRegistrationFailedRetryEvent>,
    IConsumer<ApiV2.TransactionRegistrationRetryRequestedEvent>,
    IConsumer<ApiV2.TransactionPausedEvent>,
    IConsumer<ApiV2.TransactionUnpausedEvent>
{
    public Task Consume(ConsumeContext<ApiV2.TransactionInitiatedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionCreditCompletedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionCreditFailedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionCreditFailedRetryEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionCreditRetryRequestedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionRegistrationCompletedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionRegistrationFailedRetryEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionRegistrationRetryRequestedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionPausedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApiV2.TransactionUnpausedEvent> context) =>
        RouteAndProjectAsync(context.Message, context.CancellationToken);

    private async Task RouteAndProjectAsync(object @event, CancellationToken ct)
    {
        var dashboardEvent = EventRouter.Route(@event);
        if (dashboardEvent is null)
        {
            logger.LogDebug("No projection needed for {EventType}", @event.GetType().Name);
            return;
        }

        await projector.ProjectAsync(dashboardEvent, ct);
    }
}
