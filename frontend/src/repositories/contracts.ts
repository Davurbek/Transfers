import type {
  ActionAcceptedResponse,
  AuditLog,
  AuthResponse,
  LoginCredentials,
  PermissionCreate,
  PermissionInfo,
  RoleCreate,
  RoleDetail,
  RoleListItem,
  TransactionDetail,
  TransactionSummary,
  UserCreate,
  UserDetail,
  UserInfo,
  UserListItem,
} from '@/domain/models'
import type { AuditQuery, PagedResult, TransactionQuery } from '@/domain/paging'

/** Data-access contracts. The only layer allowed to perform HTTP calls. */

export interface IAuthRepository {
  login(credentials: LoginCredentials): Promise<AuthResponse>
  refresh(): Promise<AuthResponse>
  logout(): Promise<void>
  me(): Promise<UserInfo>
}

export interface ITransactionRepository {
  search(query: TransactionQuery): Promise<PagedResult<TransactionSummary>>
  getDetail(transactionId: string): Promise<TransactionDetail>
  unpause(transactionId: string): Promise<ActionAcceptedResponse>
}

export interface IAuditRepository {
  search(query: AuditQuery): Promise<PagedResult<AuditLog>>
}

export interface IAdminRepository {
  // Users
  getAllUsers(): Promise<UserListItem[]>
  getUser(id: string): Promise<UserDetail>
  createUser(req: UserCreate): Promise<UserDetail>
  updateUser(id: string, req: Partial<UserCreate & { isActive: boolean }>): Promise<UserDetail>
  deleteUser(id: string): Promise<void>
  addUserRole(userId: string, roleId: string): Promise<void>
  removeUserRole(userId: string, roleId: string): Promise<void>
  addUserPermission(userId: string, permissionId: string): Promise<void>
  removeUserPermission(userId: string, permissionId: string): Promise<void>

  // Roles
  getAllRoles(): Promise<RoleListItem[]>
  getRole(id: string): Promise<RoleDetail>
  createRole(req: RoleCreate): Promise<RoleDetail>
  updateRole(id: string, req: RoleCreate): Promise<RoleDetail>
  deleteRole(id: string): Promise<void>
  addRolePermission(roleId: string, permissionId: string): Promise<void>
  removeRolePermission(roleId: string, permissionId: string): Promise<void>

  // Permissions
  getAllPermissions(): Promise<PermissionInfo[]>
  getPermission(id: string): Promise<PermissionInfo>
  createPermission(req: PermissionCreate): Promise<PermissionInfo>
  updatePermission(id: string, req: PermissionCreate): Promise<PermissionInfo>
  deletePermission(id: string): Promise<void>
}
