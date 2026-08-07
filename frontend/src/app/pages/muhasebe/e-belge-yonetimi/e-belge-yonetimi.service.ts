import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { KurumEBelgePolitikaRevizyonuModel, KurumEBelgePolitikasiGuncellemeRequest, KurumEBelgePolitikasiModel, KurumEBelgeReadinessModel } from './e-belge-yonetimi.dto';

/**
 * Faz 2B.11 görev md.47 - mevcut `GET policy` / `PUT policy` / `GET revizyonlar` sözleşmesi
 * DEĞİŞTİRİLMEDİ; yalnız YENİ, read-only `GET readiness` endpoint'i eklendi.
 */
@Injectable({ providedIn: 'root' })
export class EBelgeYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    private base(kurumId: number): string {
        return `${this.apiBaseUrl}/ui/kurumlar/${kurumId}/e-belge-politikasi`;
    }

    /** Politika hiç yapılandırılmamışsa backend `null` döner - bu, hata DEĞİLDİR. */
    getPolitika(kurumId: number): Observable<KurumEBelgePolitikasiModel | null> {
        return this.http.get<ApiResponse<KurumEBelgePolitikasiModel | null>>(this.base(kurumId)).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return responseEnvelope.data ?? null;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'E-belge politikasi alinamadi.');
            })
        );
    }

    updatePolitika(kurumId: number, payload: KurumEBelgePolitikasiGuncellemeRequest): Observable<KurumEBelgePolitikasiModel> {
        return this.http.put<ApiResponse<KurumEBelgePolitikasiModel>>(this.base(kurumId), payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'E-belge politikasi guncellenemedi.');
            })
        );
    }

    getReadiness(kurumId: number): Observable<KurumEBelgeReadinessModel> {
        return this.http.get<ApiResponse<KurumEBelgeReadinessModel>>(`${this.base(kurumId)}/readiness`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'E-belge hazirlik durumu alinamadi.');
            })
        );
    }

    getRevizyonlar(kurumId: number): Observable<KurumEBelgePolitikaRevizyonuModel[]> {
        return this.http.get<ApiResponse<KurumEBelgePolitikaRevizyonuModel[]>>(`${this.base(kurumId)}/revizyonlar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Degisiklik gecmisi alinamadi.');
            })
        );
    }
}
