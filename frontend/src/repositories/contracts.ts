import type {
  ActionAcceptedResponse,
  AuditLog,
  AuthResponse,
  LoginCredentials,
  TransactionDetail,
  TransactionSummary,
  UserInfo,
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
