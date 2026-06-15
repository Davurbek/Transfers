import { api } from './client'
import type {
  ActionAcceptedResponse,
  PagedResult,
  TransactionDetail,
  TransactionSummary,
} from '@/types'

export interface TransactionQuery {
  search?: string
  status?: string
  userId?: string
  page?: number
  pageSize?: number
}

export async function fetchTransactions(
  query: TransactionQuery = {},
): Promise<PagedResult<TransactionSummary>> {
  const { data } = await api.get<PagedResult<TransactionSummary>>('/transactions', {
    params: query,
  })
  return data
}

export async function fetchTransaction(transactionId: string): Promise<TransactionDetail> {
  const { data } = await api.get<TransactionDetail>(`/transactions/${transactionId}`)
  return data
}

export async function unpauseTransaction(
  transactionId: string,
): Promise<ActionAcceptedResponse> {
  const { data } = await api.post<ActionAcceptedResponse>(
    `/transactions/${transactionId}/unpause`,
  )
  return data
}
