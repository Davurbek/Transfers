// Domain models shared across every layer (mirrors the backend Domain project).

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

export interface LoginCredentials {
  username: string
  password: string
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

export interface AuditLog {
  id: string
  username: string
  actionType: string
  targetTransactionId: string | null
  timestamp: string
  ipAddress: string
  metadata: string | null
}

export interface ActionAcceptedResponse {
  message: string
  transactionId: string
  commandId: string
}
