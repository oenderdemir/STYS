import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    KdvOzetRaporModel,
    KdvRaporFilterModel,
    TevkifatOzetRaporModel
} from '../models/kdv-ozet-raporu.model';

@Injectable({ providedIn: 'root' })
export class KdvOzetRaporuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOzetRapor(filter: KdvRaporFilterModel): Observable<KdvOzetRaporModel> {
        return this.http
            .get<ApiResponse<KdvOzetRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-raporlari/ozet`,
                { params: this.toParams(filter) }
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'KDV özet raporu alınamadı.');
                })
            );
    }

    getTevkifatOzetRapor(filter: KdvRaporFilterModel): Observable<TevkifatOzetRaporModel> {
        return this.http
            .get<ApiResponse<TevkifatOzetRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-raporlari/tevkifat-ozet`,
                { params: this.toParams(filter) }
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Tevkifat özet raporu alınamadı.');
                })
            );
    }

    private toParams(filter: KdvRaporFilterModel): HttpParams {
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
        return value instanceof Date ? value.toISOString() : new Date(value).toISOString();
    }
}
