import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.dto';
import { OdaOzellikDto } from '../oda-ozellik-yonetimi/oda-ozellik-yonetimi.dto';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.dto';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { OdaDto } from './oda-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOdalarPaged(pageNumber: number, pageSize: number, query: string, tesisId: number | null, binaId: number | null): Observable<PagedResponseDto<OdaDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (binaId && binaId > 0) {
            params = params.set('binaId', binaId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<OdaDto>>>(`${this.apiBaseUrl}/ui/oda/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda listesi alinamadi.');
            })
        );
    }

    getOdalar(): Observable<OdaDto[]> {
        return this.http.get<ApiResponse<OdaDto[]>>(`${this.apiBaseUrl}/ui/oda`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda listesi alinamadi.');
            })
        );
    }

    getBinalar(): Observable<BinaDto[]> {
        return this.http.get<ApiResponse<BinaDto[]>>(`${this.apiBaseUrl}/ui/bina`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bina listesi alinamadi.');
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

    getOdaOzellikleriActive(): Observable<OdaOzellikDto[]> {
        return this.http.get<ApiResponse<OdaOzellikDto[]>>(`${this.apiBaseUrl}/ui/odaozellik/active`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda ozellik listesi alinamadi.');
            })
        );
    }

    createOda(payload: OdaDto): Observable<OdaDto> {
        return this.http.post<ApiResponse<OdaDto>>(`${this.apiBaseUrl}/ui/oda`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda olusturulamadi.');
            })
        );
    }

    updateOda(id: number, payload: OdaDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/oda/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda guncellenemedi.');
            })
        );
    }

    deleteOda(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/oda/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda silinemedi.');
            })
        );
    }
}
