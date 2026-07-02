import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { AylikOdaDolulukRaporDto } from './oda-doluluk-aylik.dto';

@Injectable({ providedIn: 'root' })
export class OdaDolulukAylikRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAylikRapor(
        tesisId: number,
        yil: number,
        ay: number,
        maskele: boolean
    ): Observable<AylikOdaDolulukRaporDto> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('yil', yil)
            .set('ay', ay)
            .set('maskele', maskele);

        return this.http
            .get<ApiResponse<AylikOdaDolulukRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/oda-doluluk-aylik`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Aylık oda doluluk raporu alınamadı.');
                })
            );
    }

    exportExcel(tesisId: number, yil: number, ay: number, maskele: boolean): Observable<Blob> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('yil', yil)
            .set('ay', ay)
            .set('maskele', maskele);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/oda-doluluk-aylik/excel`,
            { params, responseType: 'blob' }
        );
    }
}
