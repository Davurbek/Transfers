export interface UserInfo {
  id: string
  username: string
  email: string
  roles: string[]
  permissions: string[]
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: UserInfo
}

export interface TransactionSummary {
  transactionId: string
  userId: string
  recipientName: string
  amount: number
  currency: string
  corridor: string
  currentStatus: string
  isPaused: boolean
  createdAt: string
  updatedAt: string
}

export interface StatusHistory {
  fromStatus: string | null
  toStatus: string
  reason: string | null
  occurredAt: string
}

export interface CreditAttempt {
  attemptNumber: number
  gateway: string
  status: string
  failureCode: string | null
  gatewayResponse: string | null
  attemptedAt: string
}

export interface PartnerRegistration {
  partnerName: string
  status: string
  failureReason: string | null
  referenceId: string | null
  registeredAt: string
}

export interface TransactionDetail {
  summary: TransactionSummary
  statusHistory: StatusHistory[]
  creditAttempts: CreditAttempt[]
  partnerRegistrations: PartnerRegistration[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages?: number
  hasPrevious?: boolean
  hasNext?: boolean
}

export interface ActionAcceptedResponse {
  message: string
  transactionId: string
  commandId: string
}

// Permission string constants (must mirror the backend catalogue).
export const Permission = {
  TxRead: 'tx:read',
  TxUnpause: 'tx:unpause',
  TxCancel: 'tx:cancel',
  AuditRead: 'audit:read',
} as const
