import type {
  ActionAcceptedResponse,
  AuditLog,
  AuthResponse,
  TransactionDetail,
  TransactionSummary,
} from '@/domain/models'
import type { AuditQuery, PagedResult, TransactionQuery } from '@/domain/paging'

/** Application/business contracts consumed by stores and composables. */

export interface IAuthService {
  login(username: string, password: string): Promise<AuthResponse>
  refresh(): Promise<AuthResponse>
  logout(): Promise<void>
}

export interface ITransactionService {
  search(query: TransactionQuery): Promise<PagedResult<TransactionSummary>>
  getDetail(transactionId: string): Promise<TransactionDetail>
  unpause(transactionId: string): Promise<ActionAcceptedResponse>
}

export interface IAuditService {
  search(query: AuditQuery): Promise<PagedResult<AuditLog>>
}
