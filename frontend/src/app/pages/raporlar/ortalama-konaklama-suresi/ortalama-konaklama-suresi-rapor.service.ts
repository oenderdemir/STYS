import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { OrtalamaKonaklamaSuresiRaporDto } from './ortalama-konaklama-suresi-rapor.dto';

@Injectable({ providedIn: 'root' })
export class OrtalamaKonaklamaSuresiRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null
    ): Observable<OrtalamaKonaklamaSuresiRaporDto> {
        const params = this.buildParams(tesisId, baslangic, bitis, odaTipiId);

        return this.http
            .get<ApiResponse<OrtalamaKonaklamaSuresiRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/ortalama-konaklama-suresi`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Ortalama konaklama süresi raporu alınamadı.');
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
            `${this.apiBaseUrl}/api/raporlar/ortalama-konaklama-suresi/excel`,
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
