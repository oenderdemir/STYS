import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { EkHizmetTarifeDto, EkHizmetTesisDto } from './ek-hizmet-yonetimi.dto';

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

    getTarifelerByTesis(tesisId: number): Observable<EkHizmetTarifeDto[]> {
        return this.http.get<ApiResponse<EkHizmetTarifeDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet tarifeleri alinamadi.');
            })
        );
    }

    upsertTarifeler(tesisId: number, payload: EkHizmetTarifeDto[]): Observable<EkHizmetTarifeDto[]> {
        return this.http.put<ApiResponse<EkHizmetTarifeDto[]>>(`${this.apiBaseUrl}/ui/ekhizmettarife/tesis/${tesisId}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet tarifeleri kaydedilemedi.');
            })
        );
    }
}
