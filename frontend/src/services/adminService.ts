import type { PermissionCreate, PermissionInfo, RoleCreate, RoleDetail, RoleListItem, UserCreate, UserDetail, UserListItem } from '@/domain/models'
import type { IAdminRepository } from '@/repositories/contracts'
import type { IAdminService } from './contracts'

export class AdminService implements IAdminService {
  constructor(private readonly repository: IAdminRepository) {}

  getAllUsers(): Promise<UserListItem[]> { return this.repository.getAllUsers() }
  getUser(id: string): Promise<UserDetail> { return this.repository.getUser(id) }
  createUser(req: UserCreate): Promise<UserDetail> { return this.repository.createUser(req) }
  updateUser(id: string, req: Partial<UserCreate & { isActive: boolean }>): Promise<UserDetail> { return this.repository.updateUser(id, req) }
  deleteUser(id: string): Promise<void> { return this.repository.deleteUser(id) }
  addUserRole(userId: string, roleId: string): Promise<void> { return this.repository.addUserRole(userId, roleId) }
  removeUserRole(userId: string, roleId: string): Promise<void> { return this.repository.removeUserRole(userId, roleId) }
  addUserPermission(userId: string, permissionId: string): Promise<void> { return this.repository.addUserPermission(userId, permissionId) }
  removeUserPermission(userId: string, permissionId: string): Promise<void> { return this.repository.removeUserPermission(userId, permissionId) }

  getAllRoles(): Promise<RoleListItem[]> { return this.repository.getAllRoles() }
  getRole(id: string): Promise<RoleDetail> { return this.repository.getRole(id) }
  createRole(req: RoleCreate): Promise<RoleDetail> { return this.repository.createRole(req) }
  updateRole(id: string, req: RoleCreate): Promise<RoleDetail> { return this.repository.updateRole(id, req) }
  deleteRole(id: string): Promise<void> { return this.repository.deleteRole(id) }
  addRolePermission(roleId: string, permissionId: string): Promise<void> { return this.repository.addRolePermission(roleId, permissionId) }
  removeRolePermission(roleId: string, permissionId: string): Promise<void> { return this.repository.removeRolePermission(roleId, permissionId) }

  getAllPermissions(): Promise<PermissionInfo[]> { return this.repository.getAllPermissions() }
  getPermission(id: string): Promise<PermissionInfo> { return this.repository.getPermission(id) }
  createPermission(req: PermissionCreate): Promise<PermissionInfo> { return this.repository.createPermission(req) }
  updatePermission(id: string, req: PermissionCreate): Promise<PermissionInfo> { return this.repository.updatePermission(id, req) }
  deletePermission(id: string): Promise<void> { return this.repository.deletePermission(id) }
}
