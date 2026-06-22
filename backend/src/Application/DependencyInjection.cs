using Microsoft.Extensions.DependencyInjection;
using Universal.Transfers.Application.Auth;
using Universal.Transfers.Application.Transactions;
using Universal.Transfers.Application.Audit;

namespace Universal.Transfers.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
