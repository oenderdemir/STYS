import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.service';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.service';

export interface OdaDto {
    id?: number | null;
    odaNo: string;
    binaId: number;
    odaTipiId: number;
    katNo: number;
    yatakSayisi?: number | null;
    aktifMi: boolean;
}

@Injectable({ providedIn: 'root' })
export class OdaYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

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
