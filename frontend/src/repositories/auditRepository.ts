import type { AxiosInstance } from 'axios'
import type { AuditLog } from '@/domain/models'
import type { AuditQuery, PagedResult } from '@/domain/paging'
import type { IAuditRepository } from './contracts'

export class AuditRepository implements IAuditRepository {
  constructor(private readonly http: AxiosInstance) {}

  async search(query: AuditQuery): Promise<PagedResult<AuditLog>> {
    const { data } = await this.http.get<PagedResult<AuditLog>>('/audit', { params: query })
    return data
  }
}
