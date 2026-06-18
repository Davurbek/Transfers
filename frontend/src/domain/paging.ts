// Paging contracts shared across layers (mirror of backend PagedResult / PagedQuery).

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages?: number
  hasPrevious?: boolean
  hasNext?: boolean
}

export interface PagedQuery {
  page?: number
  pageSize?: number
}

export interface TransactionQuery extends PagedQuery {
  search?: string
  status?: string
  userId?: string
  isPaused?: boolean
  fromDate?: string
  toDate?: string
}

export interface AuditQuery extends PagedQuery {
  targetTransactionId?: string
  actionType?: string
  username?: string
  fromDate?: string
  toDate?: string
}
