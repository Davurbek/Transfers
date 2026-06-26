import type { AxiosInstance } from 'axios'
import type {
  PermissionCreate, PermissionInfo, RoleCreate, RoleDetail,
  RoleListItem, UserCreate, UserDetail, UserListItem,
} from '@/domain/models'
import type { IAdminRepository } from './contracts'

export class AdminRepository implements IAdminRepository {
  constructor(private readonly http: AxiosInstance) {}

  async getAllUsers(): Promise<UserListItem[]> {
    const { data } = await this.http.get<UserListItem[]>('/admin/users')
    return data
  }

  async getUser(id: string): Promise<UserDetail> {
    const { data } = await this.http.get<UserDetail>(`/admin/users/${id}`)
    return data
  }

  async createUser(req: UserCreate): Promise<UserDetail> {
    const { data } = await this.http.post<UserDetail>('/admin/users', req)
    return data
  }

  async updateUser(id: string, req: Partial<UserCreate & { isActive: boolean }>): Promise<UserDetail> {
    const { data } = await this.http.put<UserDetail>(`/admin/users/${id}`, req)
    return data
  }

  async deleteUser(id: string): Promise<void> {
    await this.http.delete(`/admin/users/${id}`)
  }

  async addUserRole(userId: string, roleId: string): Promise<void> {
    await this.http.post(`/admin/users/${userId}/roles`, { roleId })
  }

  async removeUserRole(userId: string, roleId: string): Promise<void> {
    await this.http.delete(`/admin/users/${userId}/roles/${roleId}`)
  }

  async addUserPermission(userId: string, permissionId: string): Promise<void> {
    await this.http.post(`/admin/users/${userId}/permissions`, { permissionId })
  }

  async removeUserPermission(userId: string, permissionId: string): Promise<void> {
    await this.http.delete(`/admin/users/${userId}/permissions/${permissionId}`)
  }

  async getAllRoles(): Promise<RoleListItem[]> {
    const { data } = await this.http.get<RoleListItem[]>('/admin/roles')
    return data
  }

  async getRole(id: string): Promise<RoleDetail> {
    const { data } = await this.http.get<RoleDetail>(`/admin/roles/${id}`)
    return data
  }

  async createRole(req: RoleCreate): Promise<RoleDetail> {
    const { data } = await this.http.post<RoleDetail>('/admin/roles', req)
    return data
  }

  async updateRole(id: string, req: RoleCreate): Promise<RoleDetail> {
    const { data } = await this.http.put<RoleDetail>(`/admin/roles/${id}`, req)
    return data
  }

  async deleteRole(id: string): Promise<void> {
    await this.http.delete(`/admin/roles/${id}`)
  }

  async addRolePermission(roleId: string, permissionId: string): Promise<void> {
    await this.http.post(`/admin/roles/${roleId}/permissions`, { permissionId })
  }

  async removeRolePermission(roleId: string, permissionId: string): Promise<void> {
    await this.http.delete(`/admin/roles/${roleId}/permissions/${permissionId}`)
  }

  async getAllPermissions(): Promise<PermissionInfo[]> {
    const { data } = await this.http.get<PermissionInfo[]>('/admin/permissions')
    return data
  }

  async getPermission(id: string): Promise<PermissionInfo> {
    const { data } = await this.http.get<PermissionInfo>(`/admin/permissions/${id}`)
    return data
  }

  async createPermission(req: PermissionCreate): Promise<PermissionInfo> {
    const { data } = await this.http.post<PermissionInfo>('/admin/permissions', req)
    return data
  }

  async updatePermission(id: string, req: PermissionCreate): Promise<PermissionInfo> {
    const { data } = await this.http.put<PermissionInfo>(`/admin/permissions/${id}`, req)
    return data
  }

  async deletePermission(id: string): Promise<void> {
    await this.http.delete(`/admin/permissions/${id}`)
  }
}
