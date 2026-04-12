import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { MusteriMenuModel } from './musteri-menu.model';

@Injectable({ providedIn: 'root' })
export class MusteriMenuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getByRestoranId(restoranId: number): Observable<MusteriMenuModel> {
        return this.http
            .get<ApiResponse<MusteriMenuModel>>(`${this.apiBaseUrl}/api/musteri-menu/${restoranId}`)
            .pipe(
                map((responseEnvelope) => {
                    if (responseEnvelope.success && responseEnvelope.data) {
                        return responseEnvelope.data;
                    }

                    throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Musteri menusu alinamadi.');
                })
            );
    }
}
