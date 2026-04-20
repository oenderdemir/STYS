import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { ManagerCandidateDto } from '../../core/identity';
import { IlDto } from '../il-yonetimi/il-yonetimi.dto';
import { TesisDto } from './tesis-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class TesisYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTesislerPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<TesisDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<TesisDto>>>(`${this.apiBaseUrl}/ui/tesis/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
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

    getYoneticiAdaylari(): Observable<ManagerCandidateDto[]> {
        return this.http.get<ApiResponse<ManagerCandidateDto[]>>(`${this.apiBaseUrl}/ui/yoneticiaday/tesis-yoneticileri`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Yonetici aday listesi alinamadi.');
            })
        );
    }

    getResepsiyonistAdaylari(): Observable<ManagerCandidateDto[]> {
        return this.http.get<ApiResponse<ManagerCandidateDto[]>>(`${this.apiBaseUrl}/ui/yoneticiaday/resepsiyonistler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Resepsiyonist aday listesi alinamadi.');
            })
        );
    }

    getMuhasebeciAdaylari(): Observable<ManagerCandidateDto[]> {
        return this.http.get<ApiResponse<ManagerCandidateDto[]>>(`${this.apiBaseUrl}/ui/yoneticiaday/muhasebeciler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Muhasebeci aday listesi alinamadi.');
            })
        );
    }

    createTesis(payload: TesisDto): Observable<TesisDto> {
        return this.http.post<ApiResponse<TesisDto>>(`${this.apiBaseUrl}/ui/tesis`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis olusturulamadi.');
            })
        );
    }

    updateTesis(id: number, payload: TesisDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/tesis/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis guncellenemedi.');
            })
        );
    }

    deleteTesis(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/tesis/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis silinemedi.');
            })
        );
    }
}
