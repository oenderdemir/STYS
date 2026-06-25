import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { getApiBaseUrl } from '../config';
import { ApiResponse } from '../api';

export interface TenantBrandingDto {
    kurumId?: number | null;
    tenantKey?: string | null;
    kurumAdi?: string | null;
    logoUrl?: string | null;
    applicationName: string;
}

@Injectable({ providedIn: 'root' })
export class TenantBrandingService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getBranding(host: string): Observable<TenantBrandingDto> {
        const params = new HttpParams().set('host', host);
        return this.http
            .get<ApiResponse<TenantBrandingDto>>(`${this.apiBaseUrl}/public/tenant-branding`, { params })
            .pipe(map(response => response.data ?? { applicationName: 'STYS' }));
    }

    resolveLogoUrl(logoUrl?: string | null): string {
        if (!logoUrl) {
            return 'logo.png';
        }

        if (logoUrl.startsWith('http://') || logoUrl.startsWith('https://') || logoUrl.startsWith('data:')) {
            return logoUrl;
        }

        const base = this.apiBaseUrl.replace(/\/$/, '');
        const path = logoUrl.startsWith('/') ? logoUrl : `/${logoUrl}`;
        return `${base}${path}`;
    }
}
