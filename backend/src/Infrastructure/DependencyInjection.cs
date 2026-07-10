using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Domain.Transactions.Interfaces;
using Universal.Transfers.Domain.Audit.Interfaces;
using Universal.Transfers.Domain.Inbox.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;
using Universal.Transfers.Infrastructure.Auth.Persistence;
using Universal.Transfers.Infrastructure.Transactions.Persistence;
using Universal.Transfers.Infrastructure.Transactions.Messaging;
using Universal.Transfers.Infrastructure.Audit.Persistence;
using Universal.Transfers.Infrastructure.Inbox.Persistence;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.DeadLetter.Interfaces;
using Universal.Transfers.Domain.Outbox.Interfaces;
using Universal.Transfers.Infrastructure.DeadLetter.Persistence;
using Universal.Transfers.Infrastructure.DeadLetter.Services;
using Universal.Transfers.Infrastructure.Messaging.Kafka;
using Universal.Transfers.Infrastructure.Outbox.Persistence;
using Universal.Transfers.Infrastructure.Outbox.Services;
using Universal.Transfers.Infrastructure.Common.Messaging.Eventing;
using System.Reflection;

namespace Universal.Transfers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(120));
            opt.AddInterceptors(new DomainEventInterceptor());
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();
        services.AddScoped<DeadLetterService>();

        services.AddScoped<IEventProjector, EventProjector>();

        services.AddOutboxMappers();

        return services;
    }

    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        services.AddHostedService<OutboxPublisher>();
        services.AddHostedService<DeadLetterReplayService>();
        services.AddHostedService<KafkaEventConsumer>();
        services.AddHostedService<KafkaCommandConsumer>();

        return services;
    }

    private static IServiceCollection AddOutboxMappers(this IServiceCollection services)
    {
        var mapperType = typeof(IDomainEventOutboxMapper<>);
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapperType)
                .ToList();

            foreach (var iface in interfaces)
            {
                services.AddScoped(iface, type);
            }
        }

        return services;
    }
}
