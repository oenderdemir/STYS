import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { LicenseStatusDto } from './lisans-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class LisansYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getStatus(): Observable<LicenseStatusDto> {
        return this.http.get<ApiResponse<LicenseStatusDto>>(`${this.apiBaseUrl}/ui/license/status`).pipe(
            map((res) => {
                if (res.success && res.data) {
                    return res.data;
                }
                throw new Error(tryReadApiMessage(res) ?? 'Lisans durumu alinamadi.');
            })
        );
    }

    upload(file: File): Observable<LicenseStatusDto> {
        const formData = new FormData();
        formData.append('file', file);

        return this.http.post<ApiResponse<LicenseStatusDto>>(`${this.apiBaseUrl}/ui/license/upload`, formData).pipe(
            map((res) => {
                if (res.success && res.data) {
                    return res.data;
                }
                throw new Error(tryReadApiMessage(res) ?? 'Lisans yuklenemedi.');
            })
        );
    }
}
