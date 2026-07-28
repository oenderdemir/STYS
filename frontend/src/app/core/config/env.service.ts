import { Injectable } from '@angular/core';
import { getApiBaseUrl, getAppBasePath, getEnvironment, getSessionInactivityTimeoutMs, getSessionInactivityWarningMs } from './app-runtime.config';

@Injectable({ providedIn: 'root' })
export class EnvService {
    readonly apiBaseUrl = getApiBaseUrl();
    readonly sessionInactivityTimeoutMs = getSessionInactivityTimeoutMs();
    readonly sessionInactivityWarningMs = getSessionInactivityWarningMs();
    readonly appBasePath = getAppBasePath();
    readonly environment = getEnvironment();

    buildApiUrl(path: string): string {
        const base = this.apiBaseUrl.replace(/\/$/, '');
        const normalizedPath = path.startsWith('/') ? path : `/${path}`;
        return `${base}${normalizedPath}`;
    }
}
