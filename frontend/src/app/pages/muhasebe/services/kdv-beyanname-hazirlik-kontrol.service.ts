import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../../core/api/api-response.model';
import {
    KdvBeyannameHazirlikKontrolFilterModel,
    KdvBeyannameHazirlikKontrolModel
} from '../models/kdv-beyanname-hazirlik-kontrol.model';

@Injectable({ providedIn: 'root' })
export class KdvBeyannameHazirlikKontrolService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    kontrolEt(filter: KdvBeyannameHazirlikKontrolFilterModel): Observable<KdvBeyannameHazirlikKontrolModel> {
        return this.http
            .post<ApiResponse<KdvBeyannameHazirlikKontrolModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-beyanname-hazirlik-kontrol`,
                filter
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'KDV beyanname hazırlık kontrolü yapılamadı.');
                })
            );
    }
}
