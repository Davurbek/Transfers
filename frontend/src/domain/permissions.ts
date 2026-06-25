// Permission string constants (must mirror the backend catalogue).
export const Permission = {
  TxRead: 'tx:read',
  TxUnpause: 'tx:unpause',
  TxCancel: 'tx:cancel',
  AuditRead: 'audit:read',
  Admin: 'admin',
} as const

export type PermissionCode = (typeof Permission)[keyof typeof Permission]
