import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { KonaklamaTipiDto, MisafirTipiDto, OdaFiyatDto, OdaTipiDto } from './oda-fiyat-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaFiyatYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

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

    getKonaklamaTipleri(): Observable<KonaklamaTipiDto[]> {
        return this.http.get<ApiResponse<KonaklamaTipiDto[]>>(`${this.apiBaseUrl}/ui/konaklamatipi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipi listesi alinamadi.');
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

    getOdaFiyatlariByOdaTipi(tesisOdaTipiId: number): Observable<OdaFiyatDto[]> {
        return this.http.get<ApiResponse<OdaFiyatDto[]>>(`${this.apiBaseUrl}/ui/odafiyat/odatipi/${tesisOdaTipiId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda fiyatlari alinamadi.');
            })
        );
    }

    upsertOdaFiyatlari(tesisOdaTipiId: number, fiyatlar: OdaFiyatDto[]): Observable<OdaFiyatDto[]> {
        return this.http.put<ApiResponse<OdaFiyatDto[]>>(`${this.apiBaseUrl}/ui/odafiyat/odatipi/${tesisOdaTipiId}`, fiyatlar).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda fiyatlari kaydedilemedi.');
            })
        );
    }
}
