import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../../core/api/api-response.model';
import {
    MuhasebeDashboardFilterModel,
    MuhasebeDashboardModel
} from '../models/muhasebe-dashboard.model';

@Injectable({ providedIn: 'root' })
export class MuhasebeDashboardService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getDashboard(filter: MuhasebeDashboardFilterModel): Observable<MuhasebeDashboardModel> {
        return this.http
            .post<ApiResponse<MuhasebeDashboardModel>>(
                `${this.apiBaseUrl}/ui/muhasebe/dashboard`,
                filter
            )
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Dashboard verisi alınamadı.');
                })
            );
    }
}
