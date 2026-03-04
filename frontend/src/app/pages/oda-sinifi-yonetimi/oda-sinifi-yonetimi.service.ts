import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { OdaSinifiDto } from './oda-sinifi-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaSinifiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOdaSiniflariPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<OdaSinifiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<OdaSinifiDto>>>(`${this.apiBaseUrl}/ui/odasinifi/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda sinifi listesi alinamadi.');
            })
        );
    }

    createOdaSinifi(payload: OdaSinifiDto): Observable<OdaSinifiDto> {
        return this.http.post<ApiResponse<OdaSinifiDto>>(`${this.apiBaseUrl}/ui/odasinifi`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda sinifi olusturulamadi.');
            })
        );
    }

    updateOdaSinifi(id: number, payload: OdaSinifiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odasinifi/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda sinifi guncellenemedi.');
            })
        );
    }

    deleteOdaSinifi(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odasinifi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda sinifi silinemedi.');
            })
        );
    }
}
