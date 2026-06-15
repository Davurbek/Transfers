using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.Api.Domain.Audit;
using Transfers.Dashboard.Api.Domain.Auth;
using Transfers.Dashboard.Api.Domain.Transactions;

namespace Transfers.Dashboard.Api.Data;

public class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
    // Auth
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Transactions (read replica)
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionStatusHistory> TransactionStatusHistory => Set<TransactionStatusHistory>();
    public DbSet<CreditAttempt> CreditAttempts => Set<CreditAttempt>();
    public DbSet<PartnerRegistration> PartnerRegistrations => Set<PartnerRegistration>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---------- Auth ----------
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).IsRequired();
        });

        b.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
        });

        b.Entity<Permission>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        });

        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        b.Entity<UserPermission>(e =>
        {
            e.HasKey(x => new { x.UserId, x.PermissionId });
            e.HasOne(x => x.User).WithMany(u => u.UserPermissions).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Permission).WithMany(p => p.UserPermissions).HasForeignKey(x => x.PermissionId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        // ---------- Transactions (read-optimized) ----------
        b.Entity<Transaction>(e =>
        {
            e.HasIndex(x => x.TransactionId).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.CurrentStatus);
            e.Property(x => x.TransactionId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.CurrentStatus).HasConversion<string>();
        });

        b.Entity<TransactionStatusHistory>(e =>
        {
            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => x.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
            e.Property(x => x.FromStatus).HasConversion<string>();
            e.Property(x => x.ToStatus).HasConversion<string>();
            e.HasOne(x => x.Transaction).WithMany(t => t.StatusHistory).HasForeignKey(x => x.TransactionId);
        });

        b.Entity<CreditAttempt>(e =>
        {
            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => x.AttemptedAt);
            e.Property(x => x.Gateway).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Transaction).WithMany(t => t.CreditAttempts).HasForeignKey(x => x.TransactionId);
        });

        b.Entity<PartnerRegistration>(e =>
        {
            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => x.RegisteredAt);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Transaction).WithMany(t => t.PartnerRegistrations).HasForeignKey(x => x.TransactionId);
        });

        // ---------- Audit ----------
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.TargetTransactionId);
            e.Property(x => x.ActionType).HasMaxLength(64).IsRequired();
        });
    }
}
