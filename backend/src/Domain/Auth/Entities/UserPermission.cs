namespace Universal.Transfers.Domain.Auth.Entities;

public class UserPermission
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
