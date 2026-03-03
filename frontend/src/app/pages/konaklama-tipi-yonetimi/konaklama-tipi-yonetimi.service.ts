import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { KonaklamaTipiDto } from './konaklama-tipi-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class KonaklamaTipiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getKonaklamaTipleriPaged(pageNumber: number, pageSize: number, query: string): Observable<PagedResponseDto<KonaklamaTipiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<KonaklamaTipiDto>>>(`${this.apiBaseUrl}/ui/konaklamatipi/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi listesi alinamadi.');
            })
        );
    }

    getKonaklamaTipleri(): Observable<KonaklamaTipiDto[]> {
        return this.http.get<ApiResponse<KonaklamaTipiDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi listesi alinamadi.');
            })
        );
    }

    createKonaklamaTipi(payload: KonaklamaTipiDto): Observable<KonaklamaTipiDto> {
        return this.http.post<ApiResponse<KonaklamaTipiDto>>(`${this.apiBaseUrl}/ui/konaklamatipi`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi olusturulamadi.');
            })
        );
    }

    updateKonaklamaTipi(id: number, payload: KonaklamaTipiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/konaklamatipi/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi guncellenemedi.');
            })
        );
    }

    deleteKonaklamaTipi(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/konaklamatipi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi silinemedi.');
            })
        );
    }
}
