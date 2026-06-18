using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Transfers.Dashboard.Business.Auth;
using Transfers.Dashboard.Business.Messaging;
using Transfers.Dashboard.Business.Services;

namespace Transfers.Dashboard.Business;

public static class DependencyInjection
{
    /// <summary>Registers BLL services, the token/permission services and the event projector.</summary>
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITransactionService, TransactionService>();

        // Read-path sink for consumed broker events.
        services.AddScoped<IEventProjector, EventProjector>();

        return services;
    }
}
