import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../../core/api/api-response.model';
import {
    KdvOzetRaporFilterModel,
    KdvOzetRaporModel
} from '../models/kdv-ozet-raporu.model';

@Injectable({ providedIn: 'root' })
export class KdvOzetRaporuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getOzetRapor(filter: KdvOzetRaporFilterModel): Observable<KdvOzetRaporModel> {
        return this.http
            .post<ApiResponse<KdvOzetRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-ozet-raporu`,
                filter
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
}
