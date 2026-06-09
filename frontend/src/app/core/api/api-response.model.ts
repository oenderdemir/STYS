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

    const directKeys = ['message', 'detail', 'title', 'error'];
    for (const key of directKeys) {
        const value = payload[key];
        if (typeof value === 'string' && value.trim().length > 0) {
            return value.trim();
        }

        const nestedValue = tryReadApiMessage(value);
        if (nestedValue) {
            return nestedValue;
        }
    }

    const errors = payload['errors'];
    if (!Array.isArray(errors)) {
        return null;
    }

    for (const errorItem of errors) {
        if (!isRecord(errorItem)) {
            continue;
        }

        const nestedKeys = ['detail', 'message', 'title', 'error'];
        for (const key of nestedKeys) {
            const value = errorItem[key];
            if (typeof value === 'string' && value.trim().length > 0) {
                return value.trim();
            }

            const nestedValue = tryReadApiMessage(value);
            if (nestedValue) {
                return nestedValue;
            }
        }
    }

    return null;
}
