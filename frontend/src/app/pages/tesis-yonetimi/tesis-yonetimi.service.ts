import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { IlDto } from '../il-yonetimi/il-yonetimi.service';

export interface TesisDto {
    id?: number | null;
    ad: string;
    ilId: number;
    telefon: string;
    adres: string;
    eposta?: string | null;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class TesisYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

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
