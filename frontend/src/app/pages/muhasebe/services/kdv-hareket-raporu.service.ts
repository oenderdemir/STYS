import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../../core/api/api-response.model';
import {
    KdvHareketRaporFilterModel,
    KdvHareketRaporModel
} from '../models/kdv-hareket-raporu.model';

@Injectable({ providedIn: 'root' })
export class KdvHareketRaporuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(filter: KdvHareketRaporFilterModel): Observable<KdvHareketRaporModel> {
        return this.http
            .post<ApiResponse<KdvHareketRaporModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-hareket-raporu`,
                filter
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

    exportExcel(filter: KdvHareketRaporFilterModel): Observable<Blob> {
        return this.http.post(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-hareket-raporu/export-excel`,
            filter,
            { responseType: 'blob' }
        );
    }
}
