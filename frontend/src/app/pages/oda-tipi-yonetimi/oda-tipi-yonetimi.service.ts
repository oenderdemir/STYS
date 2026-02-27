import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';

export interface OdaTipiDto {
    id?: number | null;
    ad: string;
    paylasimliMi: boolean;
    kapasite: number;
    balkonVarMi: boolean;
    klimaVarMi: boolean;
    metrekare?: number | null;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class OdaTipiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOdaTipleri(): Observable<OdaTipiDto[]> {
        return this.http.get<ApiResponse<OdaTipiDto[]>>(`${this.apiBaseUrl}/ui/odatipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi listesi alinamadi.');
            })
        );
    }

    createOdaTipi(payload: OdaTipiDto): Observable<OdaTipiDto> {
        return this.http.post<ApiResponse<OdaTipiDto>>(`${this.apiBaseUrl}/ui/odatipi`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi olusturulamadi.');
            })
        );
    }

    updateOdaTipi(id: number, payload: OdaTipiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odatipi/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi guncellenemedi.');
            })
        );
    }

    deleteOdaTipi(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odatipi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi silinemedi.');
            })
        );
    }
}
