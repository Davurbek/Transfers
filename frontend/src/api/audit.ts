import { api } from './client'
import type { PagedResult } from '@/types'

export interface AuditLog {
  id: string
  username: string
  actionType: string
  targetTransactionId: string | null
  timestamp: string
  ipAddress: string
  metadata: string | null
}

export interface AuditQuery {
  targetTransactionId?: string
  actionType?: string
  username?: string
  fromDate?: string
  toDate?: string
  page?: number
  pageSize?: number
}

export async function fetchAuditLogs(query: AuditQuery = {}): Promise<PagedResult<AuditLog>> {
  const { data } = await api.get<PagedResult<AuditLog>>('/audit', { params: query })
  return data
}
