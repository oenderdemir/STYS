export interface ApiErrorItem {
    code: string;
    field: string | null;
    detail: string;
}

export interface ApiResponse<T> {
    success: boolean;
    message: string;
    data: T | null;
    errors: ApiErrorItem[];
    traceId?: string | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
}

export function tryReadApiMessage(payload: unknown): string | null {
    if (!isRecord(payload)) {
        return null;
    }

    const message = payload['message'];
    if (typeof message === 'string' && message.trim().length > 0) {
        return message;
    }

    const errors = payload['errors'];
    if (!Array.isArray(errors)) {
        return null;
    }

    for (const errorItem of errors) {
        if (!isRecord(errorItem)) {
            continue;
        }

        const detail = errorItem['detail'];
        if (typeof detail === 'string' && detail.trim().length > 0) {
            return detail;
        }
    }

    return null;
}
