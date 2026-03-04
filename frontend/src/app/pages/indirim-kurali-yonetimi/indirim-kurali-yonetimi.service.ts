import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { IndirimKuraliDto, KonaklamaTipiLookupDto, MisafirTipiLookupDto } from './indirim-kurali-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class IndirimKuraliYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getIndirimKurallariPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<IndirimKuraliDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<IndirimKuraliDto>>>(`${this.apiBaseUrl}/ui/indirimkurali/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Indirim kurali listesi alinamadi.');
            })
        );
    }

    createIndirimKurali(payload: IndirimKuraliDto): Observable<IndirimKuraliDto> {
        return this.http.post<ApiResponse<IndirimKuraliDto>>(`${this.apiBaseUrl}/ui/indirimkurali`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Indirim kurali olusturulamadi.');
            })
        );
    }

    updateIndirimKurali(id: number, payload: IndirimKuraliDto): Observable<IndirimKuraliDto> {
        return this.http.put<ApiResponse<IndirimKuraliDto>>(`${this.apiBaseUrl}/ui/indirimkurali/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Indirim kurali guncellenemedi.');
            })
        );
    }

    deleteIndirimKurali(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/indirimkurali/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Indirim kurali silinemedi.');
            })
        );
    }

    getKonaklamaTipleri(): Observable<KonaklamaTipiLookupDto[]> {
        return this.http.get<ApiResponse<KonaklamaTipiLookupDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi listesi alinamadi.');
            })
        );
    }

    getMisafirTipleri(): Observable<MisafirTipiLookupDto[]> {
        return this.http.get<ApiResponse<MisafirTipiLookupDto[]>>(`${this.apiBaseUrl}/ui/misafirtipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi listesi alinamadi.');
            })
        );
    }

    getTesisler(): Observable<TesisDto[]> {
        return this.http.get<ApiResponse<TesisDto[]>>(`${this.apiBaseUrl}/ui/tesis`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }
}
