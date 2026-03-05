import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { OdaOzellikDto } from '../oda-ozellik-yonetimi/oda-ozellik-yonetimi.dto';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { OdaSinifiDto, OdaTipiDto } from './oda-tipi-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaTipiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOdaTipleriPaged(pageNumber: number, pageSize: number, query: string, tesisId: number | null, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<OdaTipiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<OdaTipiDto>>>(`${this.apiBaseUrl}/ui/odatipi/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi listesi alinamadi.');
            })
        );
    }

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

    getOdaSiniflari(): Observable<OdaSinifiDto[]> {
        return this.http.get<ApiResponse<OdaSinifiDto[]>>(`${this.apiBaseUrl}/ui/odasinifi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda sinifi listesi alinamadi.');
            })
        );
    }

    getOdaOzellikleriForOdaTipi(): Observable<OdaOzellikDto[]> {
        return this.http.get<ApiResponse<OdaOzellikDto[]>>(`${this.apiBaseUrl}/ui/odaozellik/active-for-odatipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda ozellik listesi alinamadi.');
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
