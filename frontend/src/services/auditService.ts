import type { AuditLog } from '@/domain/models'
import type { AuditQuery, PagedResult } from '@/domain/paging'
import type { IAuditRepository } from '@/repositories/contracts'
import type { IAuditService } from './contracts'
import { normalizeQuery, toIso } from './queryUtils'

const DEFAULT_PAGE_SIZE = 50

export class AuditService implements IAuditService {
  constructor(private readonly repository: IAuditRepository) {}

  search(query: AuditQuery): Promise<PagedResult<AuditLog>> {
    const normalized = normalizeQuery<AuditQuery>(
      {
        ...query,
        fromDate: toIso(query.fromDate),
        toDate: toIso(query.toDate),
      },
      DEFAULT_PAGE_SIZE,
    )
    return this.repository.search(normalized)
  }
}
