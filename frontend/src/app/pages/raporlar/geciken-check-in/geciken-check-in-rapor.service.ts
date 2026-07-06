import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { GecikenCheckInRaporDto } from './geciken-check-in-rapor.dto';

@Injectable({ providedIn: 'root' })
export class GecikenCheckInRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        referansTarihi: string,
        odaTipiId: number | null,
        gecikmeDurumu: string
    ): Observable<GecikenCheckInRaporDto> {
        const params = this.buildParams(tesisId, referansTarihi, odaTipiId, gecikmeDurumu);

        return this.http
            .get<ApiResponse<GecikenCheckInRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/geciken-check-in`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Geciken check-in raporu alınamadı.');
                })
            );
    }

    exportExcel(
        tesisId: number,
        referansTarihi: string,
        odaTipiId: number | null,
        gecikmeDurumu: string
    ): Observable<Blob> {
        const params = this.buildParams(tesisId, referansTarihi, odaTipiId, gecikmeDurumu);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/geciken-check-in/excel`,
            { params, responseType: 'blob' }
        );
    }

    private buildParams(
        tesisId: number,
        referansTarihi: string,
        odaTipiId: number | null,
        gecikmeDurumu: string
    ): HttpParams {
        let params = new HttpParams()
            .set('tesisId', tesisId)
            .set('referansTarihi', referansTarihi)
            .set('gecikmeDurumu', gecikmeDurumu);

        if (odaTipiId) {
            params = params.set('odaTipiId', odaTipiId);
        }

        return params;
    }
}
