import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { EkHizmetDto, EkHizmetTarifeDto, EkHizmetTesisAtamaDto, EkHizmetTesisDto, GlobalEkHizmetTanimiDto } from './ek-hizmet-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class EkHizmetYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTesisler(): Observable<EkHizmetTesisDto[]> {
        return this.http.get<ApiResponse<EkHizmetTesisDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    getGlobalTanimlar(): Observable<GlobalEkHizmetTanimiDto[]> {
        return this.http.get<ApiResponse<GlobalEkHizmetTanimiDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/global-tanimlar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global ek hizmet tanimlari alinamadi.');
            })
        );
    }

    getGlobalTanimById(id: number): Observable<GlobalEkHizmetTanimiDto> {
        return this.http.get<ApiResponse<GlobalEkHizmetTanimiDto>>(`${this.apiBaseUrl}/ui/ekhizmettarife/global-tanimlar/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global ek hizmet tanimi alinamadi.');
            })
        );
    }

    createGlobalTanim(payload: GlobalEkHizmetTanimiDto): Observable<GlobalEkHizmetTanimiDto> {
        return this.http.post<ApiResponse<GlobalEkHizmetTanimiDto>>(`${this.apiBaseUrl}/ui/ekhizmettarife/global-tanimlar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global ek hizmet tanimi olusturulamadi.');
            })
        );
    }

    updateGlobalTanim(id: number, payload: GlobalEkHizmetTanimiDto): Observable<GlobalEkHizmetTanimiDto> {
        return this.http.put<ApiResponse<GlobalEkHizmetTanimiDto>>(`${this.apiBaseUrl}/ui/ekhizmettarife/global-tanimlar/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global ek hizmet tanimi guncellenemedi.');
            })
        );
    }

    deleteGlobalTanim(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/ekhizmettarife/global-tanimlar/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global ek hizmet tanimi silinemedi.');
            })
        );
    }

    getTesisAtamalari(tesisId: number): Observable<EkHizmetTesisAtamaDto[]> {
        return this.http.get<ApiResponse<EkHizmetTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}/atamalar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet atamalari alinamadi.');
            })
        );
    }

    kaydetTesisAtamalari(tesisId: number, globalEkHizmetTanimiIds: number[]): Observable<EkHizmetTesisAtamaDto[]> {
        return this.http.put<ApiResponse<EkHizmetTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}/atamalar`, { globalEkHizmetTanimiIds }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet atamalari kaydedilemedi.');
            })
        );
    }

    getHizmetlerByTesis(tesisId: number): Observable<EkHizmetDto[]> {
        return this.http.get<ApiResponse<EkHizmetDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}/hizmetler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet tanimlari alinamadi.');
            })
        );
    }

    getTarifelerByTesis(tesisId: number): Observable<EkHizmetTarifeDto[]> {
        return this.http.get<ApiResponse<EkHizmetTarifeDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}/tarifeler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet tarifeleri alinamadi.');
            })
        );
    }

    upsertTarifeler(tesisId: number, payload: EkHizmetTarifeDto[]): Observable<EkHizmetTarifeDto[]> {
        return this.http.put<ApiResponse<EkHizmetTarifeDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}/tarifeler`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet tarifeleri kaydedilemedi.');
            })
        );
    }
}
