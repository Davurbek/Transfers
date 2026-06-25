import { http } from '@/infrastructure/httpClient'

import { AuthRepository } from '@/repositories/authRepository'
import { TransactionRepository } from '@/repositories/transactionRepository'
import { AuditRepository } from '@/repositories/auditRepository'
import { AdminRepository } from '@/repositories/adminRepository'

import { AuthService } from '@/services/authService'
import { TransactionService } from '@/services/transactionService'
import { AuditService } from '@/services/auditService'
import { AdminService } from '@/services/adminService'

import type { IAuthService, IAuditService, ITransactionService, IAdminService } from '@/services/contracts'

/**
 * Composition root (the front-end analogue of the backend's AddDataAccess /
 * AddBusiness DI registration). Wires the layers once and exposes services to
 * the presentation layer (stores / composables). Lower layers depend only on
 * abstractions and receive their dependencies by constructor injection.
 */

// Infrastructure -> Repositories (DAL)
const authRepository = new AuthRepository(http)
const transactionRepository = new TransactionRepository(http)
const auditRepository = new AuditRepository(http)

// Repositories -> Services (BLL)
export const authService: IAuthService = new AuthService(authRepository)
export const transactionService: ITransactionService = new TransactionService(transactionRepository)
export const auditService: IAuditService = new AuditService(auditRepository)
const adminRepository = new AdminRepository(http)
export const adminService: IAdminService = new AdminService(adminRepository)
