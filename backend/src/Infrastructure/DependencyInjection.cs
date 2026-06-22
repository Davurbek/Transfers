using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Domain.Transactions.Interfaces;
using Universal.Transfers.Domain.Audit.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;
using Universal.Transfers.Infrastructure.Auth.Persistence;
using Universal.Transfers.Infrastructure.Transactions.Persistence;
using Universal.Transfers.Infrastructure.Transactions.Messaging;
using Universal.Transfers.Infrastructure.Audit.Persistence;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Infrastructure.Messaging.Kafka;

namespace Universal.Transfers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Dashboard");

        services.AddDbContext<AppDbContext>(opt => opt
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(120)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<IEventProjector, EventProjector>();

        return services;
    }

    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        services.AddSingleton<ICommandPublisher, KafkaCommandPublisher>();
        services.AddHostedService<KafkaEventConsumer>();

        return services;
    }
}
