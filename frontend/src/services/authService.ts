import type { AuthResponse } from '@/domain/models'
import type { IAuthRepository } from '@/repositories/contracts'
import type { IAuthService } from './contracts'

/**
 * Business logic for authentication. Depends on the repository abstraction
 * (constructor injection), never on the HTTP client directly.
 */
export class AuthService implements IAuthService {
  constructor(private readonly repository: IAuthRepository) {}

  login(username: string, password: string): Promise<AuthResponse> {
    return this.repository.login({
      username: username.trim(),
      password,
    })
  }

  refresh(): Promise<AuthResponse> {
    return this.repository.refresh()
  }

  logout(): Promise<void> {
    return this.repository.logout()
  }
}
