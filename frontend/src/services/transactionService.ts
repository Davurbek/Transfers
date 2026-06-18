import type {
  ActionAcceptedResponse,
  TransactionDetail,
  TransactionSummary,
} from '@/domain/models'
import type { PagedResult, TransactionQuery } from '@/domain/paging'
import type { ITransactionRepository } from '@/repositories/contracts'
import type { ITransactionService } from './contracts'
import { normalizeQuery, toIso } from './queryUtils'

const DEFAULT_PAGE_SIZE = 20

export class TransactionService implements ITransactionService {
  constructor(private readonly repository: ITransactionRepository) {}

  search(query: TransactionQuery): Promise<PagedResult<TransactionSummary>> {
    const normalized = normalizeQuery<TransactionQuery>(
      {
        ...query,
        search: query.search?.trim(),
        fromDate: toIso(query.fromDate),
        toDate: toIso(query.toDate),
      },
      DEFAULT_PAGE_SIZE,
    )
    return this.repository.search(normalized)
  }

  getDetail(transactionId: string): Promise<TransactionDetail> {
    return this.repository.getDetail(transactionId)
  }

  unpause(transactionId: string): Promise<ActionAcceptedResponse> {
    return this.repository.unpause(transactionId)
  }
}
