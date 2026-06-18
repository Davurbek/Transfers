import type { AxiosInstance } from 'axios'
import type { AuthResponse, LoginCredentials, UserInfo } from '@/domain/models'
import type { IAuthRepository } from './contracts'

export class AuthRepository implements IAuthRepository {
  constructor(private readonly http: AxiosInstance) {}

  async login(credentials: LoginCredentials): Promise<AuthResponse> {
    const { data } = await this.http.post<AuthResponse>('/auth/login', credentials)
    return data
  }

  async refresh(): Promise<AuthResponse> {
    const { data } = await this.http.post<AuthResponse>('/auth/refresh')
    return data
  }

  async logout(): Promise<void> {
    await this.http.post('/auth/logout')
  }

  async me(): Promise<UserInfo> {
    const { data } = await this.http.get<UserInfo>('/auth/me')
    return data
  }
}
