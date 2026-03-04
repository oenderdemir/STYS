import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.dto';
import { IsletmeAlaniDto, IsletmeAlaniSinifiDto } from './isletme-alani-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class IsletmeAlaniYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAlanlarPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<IsletmeAlaniDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<IsletmeAlaniDto>>>(`${this.apiBaseUrl}/ui/isletmealani/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani listesi alinamadi.');
            })
        );
    }

    getAlanlar(): Observable<IsletmeAlaniDto[]> {
        return this.http.get<ApiResponse<IsletmeAlaniDto[]>>(`${this.apiBaseUrl}/ui/isletmealani`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani listesi alinamadi.');
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

    getSiniflar(onlyActive = true): Observable<IsletmeAlaniSinifiDto[]> {
        const params = new HttpParams().set('onlyActive', onlyActive);
        return this.http.get<ApiResponse<IsletmeAlaniSinifiDto[]>>(`${this.apiBaseUrl}/ui/isletmealani/siniflar`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani sinif listesi alinamadi.');
            })
        );
    }

    getSiniflarPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<IsletmeAlaniSinifiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<IsletmeAlaniSinifiDto>>>(`${this.apiBaseUrl}/ui/isletmealani/siniflar/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani sinif listesi alinamadi.');
            })
        );
    }

    createSinif(payload: IsletmeAlaniSinifiDto): Observable<IsletmeAlaniSinifiDto> {
        return this.http.post<ApiResponse<IsletmeAlaniSinifiDto>>(`${this.apiBaseUrl}/ui/isletmealani/siniflar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani sinifi olusturulamadi.');
            })
        );
    }

    updateSinif(id: number, payload: IsletmeAlaniSinifiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/isletmealani/siniflar/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani sinifi guncellenemedi.');
            })
        );
    }

    deleteSinif(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/isletmealani/siniflar/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani sinifi silinemedi.');
            })
        );
    }

    createAlan(payload: IsletmeAlaniDto): Observable<IsletmeAlaniDto> {
        return this.http.post<ApiResponse<IsletmeAlaniDto>>(`${this.apiBaseUrl}/ui/isletmealani`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani olusturulamadi.');
            })
        );
    }

    updateAlan(id: number, payload: IsletmeAlaniDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/isletmealani/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani guncellenemedi.');
            })
        );
    }

    deleteAlan(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/isletmealani/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alani silinemedi.');
            })
        );
    }
}
