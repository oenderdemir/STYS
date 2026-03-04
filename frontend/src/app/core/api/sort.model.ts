import { LazyLoadPayload } from './lazy-load-payload.model';

export type SortDirection = 'asc' | 'desc';

export interface PageSort {
    sortBy: string;
    sortDir: SortDirection;
}

export function resolveSortFromLazyPayload(payload: LazyLoadPayload, fallbackSortBy: string, fallbackSortDir: SortDirection = 'asc'): PageSort {
    const rawField = Array.isArray(payload.sortField) ? payload.sortField[0] : payload.sortField;
    const normalizedField = (rawField ?? '').toString().trim();
    const sortBy = normalizedField.length > 0 ? normalizedField : fallbackSortBy;
    const sortDir = payload.sortOrder === -1 ? 'desc' : payload.sortOrder === 1 ? 'asc' : fallbackSortDir;
    return { sortBy, sortDir };
}
