using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Transfers.Dashboard.DataAccess.Context;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.DataAccess.Repositories.Implementations;

namespace Transfers.Dashboard.DataAccess;

public static class DependencyInjection
{
    /// <summary>Registers the isolated Dashboard DB context, repositories and unit of work.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DashboardDbContext>(opt => opt.UseSqlite(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        return services;
    }
}
