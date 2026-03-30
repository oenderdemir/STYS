import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { KonaklamaTipiDto, KonaklamaTipiTesisAtamaDto, KonaklamaTipiTesisIcerikOverrideDto, KonaklamaTipiYonetimBaglamDto } from './konaklama-tipi-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class KonaklamaTipiYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getYonetimBaglam(): Observable<KonaklamaTipiYonetimBaglamDto> {
        return this.http.get<ApiResponse<KonaklamaTipiYonetimBaglamDto>>(`${this.apiBaseUrl}/ui/konaklamatipi/yonetim-baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi yonetim baglami alinamadi.');
            })
        );
    }

    getKonaklamaTipleriPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<KonaklamaTipiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
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

    getKonaklamaTipleri(tesisId?: number | null): Observable<KonaklamaTipiDto[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<KonaklamaTipiDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi listesi alinamadi.');
            })
        );
    }

    getKonaklamaTipiById(id: number): Observable<KonaklamaTipiDto> {
        return this.http.get<ApiResponse<KonaklamaTipiDto>>(`${this.apiBaseUrl}/ui/konaklamatipi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi detayi alinamadi.');
            })
        );
    }

    getTesisAtamalari(tesisId: number): Observable<KonaklamaTipiTesisAtamaDto[]> {
        return this.http.get<ApiResponse<KonaklamaTipiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi/tesis/${tesisId}/atamalar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis konaklama tipi atamalari alinamadi.');
            })
        );
    }

    kaydetTesisAtamalari(tesisId: number, konaklamaTipiIds: number[]): Observable<KonaklamaTipiTesisAtamaDto[]> {
        return this.http.put<ApiResponse<KonaklamaTipiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi/tesis/${tesisId}/atamalar`, { konaklamaTipiIds }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis konaklama tipi atamalari kaydedilemedi.');
            })
        );
    }

    getTesisIcerikOverride(tesisId: number, konaklamaTipiId: number): Observable<KonaklamaTipiTesisIcerikOverrideDto[]> {
        return this.http.get<ApiResponse<KonaklamaTipiTesisIcerikOverrideDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi/tesis/${tesisId}/atamalar/${konaklamaTipiId}/icerik-override`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis icerik override bilgileri alinamadi.');
            })
        );
    }

    kaydetTesisIcerikOverride(tesisId: number, konaklamaTipiId: number, icerikKalemleri: KonaklamaTipiTesisIcerikOverrideDto[]): Observable<KonaklamaTipiTesisIcerikOverrideDto[]> {
        return this.http.put<ApiResponse<KonaklamaTipiTesisIcerikOverrideDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi/tesis/${tesisId}/atamalar/${konaklamaTipiId}/icerik-override`, { icerikKalemleri }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis icerik override bilgileri kaydedilemedi.');
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
