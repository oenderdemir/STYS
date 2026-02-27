import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.service';

export interface IsletmeAlaniDto {
    id?: number | null;
    ad: string;
    binaId: number;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class IsletmeAlaniYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

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
