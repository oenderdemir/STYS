import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { toLocalDateTimeString } from '../../../core/utils/date-time.util';
import {
    KdvHareketRaporFilterModel,
    KdvHareketRaporModel,
    TevkifatHareketRaporModel
} from '../models/kdv-hareket-raporu.model';

@Injectable({ providedIn: 'root' })
export class KdvHareketRaporuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(filter: KdvHareketRaporFilterModel): Observable<KdvHareketRaporModel> {
        return this.http
            .get<ApiResponse<KdvHareketRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-raporlari/hareketler`,
                { params: this.toParams(filter) }
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'KDV hareket raporu alınamadı.');
                })
            );
    }

    getTevkifatRapor(filter: KdvHareketRaporFilterModel): Observable<TevkifatHareketRaporModel> {
        return this.http
            .get<ApiResponse<TevkifatHareketRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-raporlari/tevkifat-hareketler`,
                { params: this.toParams(filter) }
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Tevkifat hareket raporu alınamadı.');
                })
            );
    }

    private toParams(filter: KdvHareketRaporFilterModel): HttpParams {
        let params = new HttpParams();

        if (filter.tesisId != null) {
            params = params.set('TesisId', filter.tesisId);
        }
        if (filter.baslangicTarihi) {
            params = params.set('BaslangicTarihi', this.toIso(filter.baslangicTarihi));
        }
        if (filter.bitisTarihi) {
            params = params.set('BitisTarihi', this.toIso(filter.bitisTarihi));
        }
        if (filter.belgeYonu) {
            params = params.set('BelgeYonu', filter.belgeYonu);
        }

        params = params.set('IstisnalarDahilMi', String(filter.istisnalarDahilMi));
        params = params.set('TevkifatDahilMi', String(filter.tevkifatDahilMi));
        return params;
    }

    private toIso(value: Date | string): string {
        const date = value instanceof Date ? value : new Date(value);
        return toLocalDateTimeString(date) ?? '';
    }
}
