import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.service';

export interface BinaDto {
    id?: number | null;
    ad: string;
    tesisId: number;
    katSayisi: number;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class BinaYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

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

    createBina(payload: BinaDto): Observable<BinaDto> {
        return this.http.post<ApiResponse<BinaDto>>(`${this.apiBaseUrl}/ui/bina`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bina olusturulamadi.');
            })
        );
    }

    updateBina(id: number, payload: BinaDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/bina/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bina guncellenemedi.');
            })
        );
    }

    deleteBina(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/bina/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bina silinemedi.');
            })
        );
    }
}
