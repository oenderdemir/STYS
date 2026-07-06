import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { OdaTipiDolulukRaporDto } from './oda-tipi-doluluk-rapor.dto';

@Injectable({ providedIn: 'root' })
export class OdaTipiDolulukRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null
    ): Observable<OdaTipiDolulukRaporDto> {
        const params = this.buildParams(tesisId, baslangic, bitis, odaTipiId);

        return this.http
            .get<ApiResponse<OdaTipiDolulukRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/oda-tipi-doluluk`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Oda tipi bazlı doluluk raporu alınamadı.');
                })
            );
    }

    exportExcel(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null
    ): Observable<Blob> {
        const params = this.buildParams(tesisId, baslangic, bitis, odaTipiId);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/oda-tipi-doluluk/excel`,
            { params, responseType: 'blob' }
        );
    }

    private buildParams(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null
    ): HttpParams {
        let params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangic', baslangic)
            .set('bitis', bitis);

        if (odaTipiId) {
            params = params.set('odaTipiId', odaTipiId);
        }

        return params;
    }
}
