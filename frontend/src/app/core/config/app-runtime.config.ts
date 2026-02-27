interface RuntimeEnvironment {
    apiBaseUrl?: string;
    sessionInactivityTimeoutMs?: number;
}

declare global {
    interface Window {
        __env?: RuntimeEnvironment;
    }
}

const defaultApiBaseUrl = '/api';
const defaultSessionInactivityTimeoutMs = 10 * 60 * 1000;

export function getApiBaseUrl(): string {
    if (typeof window === 'undefined') {
        return defaultApiBaseUrl;
    }

    const configuredApiBaseUrl = window.__env?.apiBaseUrl;
    if (typeof configuredApiBaseUrl !== 'string' || configuredApiBaseUrl.trim().length === 0) {
        return defaultApiBaseUrl;
    }

    return configuredApiBaseUrl.trim();
}

export function getSessionInactivityTimeoutMs(): number {
    if (typeof window === 'undefined') {
        return defaultSessionInactivityTimeoutMs;
    }

    const configuredTimeout = window.__env?.sessionInactivityTimeoutMs;
    if (typeof configuredTimeout !== 'number' || !Number.isFinite(configuredTimeout) || configuredTimeout <= 0) {
        return defaultSessionInactivityTimeoutMs;
    }

    return configuredTimeout;
}
