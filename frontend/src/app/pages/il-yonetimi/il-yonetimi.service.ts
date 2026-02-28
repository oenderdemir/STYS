import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';

export interface IlDto {
    id?: number | null;
    ad: string;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class IlYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getIllerPaged(pageNumber: number, pageSize: number, query: string): Observable<PagedResponseDto<IlDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<IlDto>>>(`${this.apiBaseUrl}/ui/il/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Il listesi alinamadi.');
            })
        );
    }

    getIller(): Observable<IlDto[]> {
        return this.http.get<ApiResponse<IlDto[]>>(`${this.apiBaseUrl}/ui/il`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Il listesi alinamadi.');
            })
        );
    }

    createIl(payload: IlDto): Observable<IlDto> {
        return this.http.post<ApiResponse<IlDto>>(`${this.apiBaseUrl}/ui/il`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Il olusturulamadi.');
            })
        );
    }

    updateIl(id: number, payload: IlDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/il/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Il guncellenemedi.');
            })
        );
    }

    deleteIl(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/il/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Il silinemedi.');
            })
        );
    }
}
