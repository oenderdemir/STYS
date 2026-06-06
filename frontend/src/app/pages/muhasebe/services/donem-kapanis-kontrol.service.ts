import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import {
    DonemKapanisKontrolFilterModel,
    DonemKapanisKontrolModel
} from '../models/donem-kapanis-kontrol.model';
import { getApiBaseUrl } from '@/app/core/config/app-runtime.config';

@Injectable({ providedIn: 'root' })
export class DonemKapanisKontrolService {
    private readonly apiBaseUrl =getApiBaseUrl();;

    constructor(private readonly http: HttpClient) { }

    kontrolEt(filter: DonemKapanisKontrolFilterModel): Observable<DonemKapanisKontrolModel> {
        return this.http.post<ApiResponse<DonemKapanisKontrolModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/donem-kapanis/kontrol`,
            filter
        ).pipe(
            map(envelope => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Dönem kapanış kontrolü alınamadı.');
            })
        );
    }
}
