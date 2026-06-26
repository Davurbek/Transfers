namespace Universal.Transfers.Application.Admin.DTOs;

public record UserListItemDto(Guid Id, string Username, string Email, bool IsActive, DateTimeOffset CreatedAt, List<string> Roles);
public record UserDetailDto(Guid Id, string Username, string Email, bool IsActive, DateTimeOffset CreatedAt, List<RoleDto> Roles, List<PermissionDto> Permissions);
public record UserCreateRequest(string Username, string Email, string Password, List<Guid>? RoleIds = null);
public record UserUpdateRequest(string Username, string Email, bool IsActive);
public record UserRoleRequest(Guid RoleId);
public record RoleListItemDto(Guid Id, string Name, string? Description);
public record RoleDetailDto(Guid Id, string Name, string? Description, List<PermissionDto> Permissions);
public record RoleCreateRequest(string Name, string? Description);
public record RoleUpdateRequest(string Name, string? Description);
public record RolePermissionRequest(Guid PermissionId);
public record UserPermissionRequest(Guid PermissionId);
public record PermissionDto(Guid Id, string Code, string? Description);
public record PermissionCreateRequest(string Code, string? Description);
public record PermissionUpdateRequest(string Code, string? Description);
public record RoleDto(Guid Id, string Name, string? Description);
