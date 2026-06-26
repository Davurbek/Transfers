using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Universal.Transfers.Application.Admin;
using Universal.Transfers.Application.Admin.DTOs;

namespace Universal.Transfers.Api.Admin;

[Authorize]
[ApiController]
[Route("admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    // ── Users ──────────────────────────────────────────
    [Authorize(Policy = "permission:admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken ct)
        => Ok(await adminService.GetAllUsersAsync(ct));

    [Authorize(Policy = "permission:admin")]
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await adminService.GetUserByIdAsync(id, ct);
        if (user is null) return NotFound();
        return Ok(user);
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("users")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request, CancellationToken ct)
    {
        try
        {
            var user = await adminService.CreateUserAsync(request, ct);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPut("users/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UserUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var user = await adminService.UpdateUserAsync(id, request, ct);
            return Ok(user);
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("users/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        try
        {
            await adminService.DeleteUserAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("users/{userId:guid}/roles")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> AddUserRole(Guid userId, [FromBody] UserRoleRequest request, CancellationToken ct)
    {
        try
        {
            await adminService.AddUserRoleAsync(userId, request.RoleId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("users/{userId:guid}/roles/{roleId:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> RemoveUserRole(Guid userId, Guid roleId, CancellationToken ct)
    {
        await adminService.RemoveUserRoleAsync(userId, roleId, ct);
        return NoContent();
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("users/{userId:guid}/permissions")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> AddUserPermission(Guid userId, [FromBody] UserPermissionRequest request, CancellationToken ct)
    {
        try
        {
            await adminService.AddUserPermissionAsync(userId, request.PermissionId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("users/{userId:guid}/permissions/{permissionId:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> RemoveUserPermission(Guid userId, Guid permissionId, CancellationToken ct)
    {
        await adminService.RemoveUserPermissionAsync(userId, permissionId, ct);
        return NoContent();
    }

    // ── Roles ──────────────────────────────────────────
    [Authorize(Policy = "permission:admin")]
    [HttpGet("roles")]
    public async Task<IActionResult> GetAllRoles(CancellationToken ct)
        => Ok(await adminService.GetAllRolesAsync(ct));

    [Authorize(Policy = "permission:admin")]
    [HttpGet("roles/{id:guid}")]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken ct)
    {
        var role = await adminService.GetRoleByIdAsync(id, ct);
        if (role is null) return NotFound();
        return Ok(role);
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("roles")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateRole([FromBody] RoleCreateRequest request, CancellationToken ct)
    {
        try
        {
            var role = await adminService.CreateRoleAsync(request, ct);
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPut("roles/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] RoleUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var role = await adminService.UpdateRoleAsync(id, request, ct);
            return Ok(role);
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("roles/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        try
        {
            await adminService.DeleteRoleAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("roles/{roleId:guid}/permissions")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> AddRolePermission(Guid roleId, [FromBody] RolePermissionRequest request, CancellationToken ct)
    {
        try
        {
            await adminService.AddRolePermissionAsync(roleId, request.PermissionId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("roles/{roleId:guid}/permissions/{permissionId:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> RemoveRolePermission(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        await adminService.RemoveRolePermissionAsync(roleId, permissionId, ct);
        return NoContent();
    }

    // ── Permissions ────────────────────────────────────
    [Authorize(Policy = "permission:admin")]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetAllPermissions(CancellationToken ct)
        => Ok(await adminService.GetAllPermissionsAsync(ct));

    [Authorize(Policy = "permission:admin")]
    [HttpGet("permissions/{id:guid}")]
    public async Task<IActionResult> GetPermission(Guid id, CancellationToken ct)
    {
        var perm = await adminService.GetPermissionByIdAsync(id, ct);
        if (perm is null) return NotFound();
        return Ok(perm);
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPost("permissions")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreatePermission([FromBody] PermissionCreateRequest request, CancellationToken ct)
    {
        try
        {
            var perm = await adminService.CreatePermissionAsync(request, ct);
            return CreatedAtAction(nameof(GetPermission), new { id = perm.Id }, perm);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpPut("permissions/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdatePermission(Guid id, [FromBody] PermissionUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var perm = await adminService.UpdatePermissionAsync(id, request, ct);
            return Ok(perm);
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [Authorize(Policy = "permission:admin")]
    [HttpDelete("permissions/{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeletePermission(Guid id, CancellationToken ct)
    {
        try
        {
            await adminService.DeletePermissionAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }
}
