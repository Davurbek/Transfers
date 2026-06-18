import type { AxiosInstance } from 'axios'
import type {
  ActionAcceptedResponse,
  TransactionDetail,
  TransactionSummary,
} from '@/domain/models'
import type { PagedResult, TransactionQuery } from '@/domain/paging'
import type { ITransactionRepository } from './contracts'

export class TransactionRepository implements ITransactionRepository {
  constructor(private readonly http: AxiosInstance) {}

  async search(query: TransactionQuery): Promise<PagedResult<TransactionSummary>> {
    const { data } = await this.http.get<PagedResult<TransactionSummary>>('/transactions', {
      params: query,
    })
    return data
  }

  async getDetail(transactionId: string): Promise<TransactionDetail> {
    const { data } = await this.http.get<TransactionDetail>(`/transactions/${transactionId}`)
    return data
  }

  async unpause(transactionId: string): Promise<ActionAcceptedResponse> {
    const { data } = await this.http.post<ActionAcceptedResponse>(
      `/transactions/${transactionId}/unpause`,
    )
    return data
  }
}
