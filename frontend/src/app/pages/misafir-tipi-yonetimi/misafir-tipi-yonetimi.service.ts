import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { MisafirTipiDto, MisafirTipiTesisAtamaDto, MisafirTipiYonetimBaglamDto } from './misafir-tipi-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class MisafirTipiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getMisafirTipleriPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<MisafirTipiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<MisafirTipiDto>>>(`${this.apiBaseUrl}/ui/misafirtipi/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
 
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi listesi alinamadi.');
            })
        );
    }

    getMisafirTipleri(): Observable<MisafirTipiDto[]> {
        return this.http.get<ApiResponse<MisafirTipiDto[]>>(`${this.apiBaseUrl}/ui/misafirtipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi listesi alinamadi.');
            })
        );
    }

    getMisafirTipleriByTesis(tesisId: number): Observable<MisafirTipiDto[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<MisafirTipiDto[]>>(`${this.apiBaseUrl}/ui/misafirtipi`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi listesi alinamadi.');
            })
        );
    }

    getYonetimBaglam(): Observable<MisafirTipiYonetimBaglamDto> {
        return this.http.get<ApiResponse<MisafirTipiYonetimBaglamDto>>(`${this.apiBaseUrl}/ui/misafirtipi/yonetim-baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi yonetim baglami alinamadi.');
            })
        );
    }

    getTesisAtamalari(tesisId: number): Observable<MisafirTipiTesisAtamaDto[]> {
        return this.http.get<ApiResponse<MisafirTipiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/misafirtipi/tesis/${tesisId}/atamalar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis misafir tipi atamalari alinamadi.');
            })
        );
    }

    kaydetTesisAtamalari(tesisId: number, misafirTipiIds: number[]): Observable<MisafirTipiTesisAtamaDto[]> {
        return this.http.put<ApiResponse<MisafirTipiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/misafirtipi/tesis/${tesisId}/atamalar`, { misafirTipiIds }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis misafir tipi atamalari kaydedilemedi.');
            })
        );
    }

    getMisafirTipiById(id: number): Observable<MisafirTipiDto> {
        return this.http.get<ApiResponse<MisafirTipiDto>>(`${this.apiBaseUrl}/ui/misafirtipi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi detayi alinamadi.');
            })
        );
    }

    createMisafirTipi(payload: MisafirTipiDto): Observable<MisafirTipiDto> {
        return this.http.post<ApiResponse<MisafirTipiDto>>(`${this.apiBaseUrl}/ui/misafirtipi`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi olusturulamadi.');
            })
        );
    }

    updateMisafirTipi(id: number, payload: MisafirTipiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/misafirtipi/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi guncellenemedi.');
            })
        );
    }

    deleteMisafirTipi(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/misafirtipi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipi silinemedi.');
            })
        );
    }
}
