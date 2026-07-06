import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { RezervasyonDurumDagilimiRaporDto } from './rezervasyon-durum-dagilimi-rapor.dto';

@Injectable({ providedIn: 'root' })
export class RezervasyonDurumDagilimiRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null,
        durum: string | null
    ): Observable<RezervasyonDurumDagilimiRaporDto> {
        const params = this.buildParams(tesisId, baslangic, bitis, odaTipiId, durum);

        return this.http
            .get<ApiResponse<RezervasyonDurumDagilimiRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/rezervasyon-durum-dagilimi`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Rezervasyon durum dağılımı raporu alınamadı.');
                })
            );
    }

    exportExcel(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null,
        durum: string | null
    ): Observable<Blob> {
        const params = this.buildParams(tesisId, baslangic, bitis, odaTipiId, durum);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/rezervasyon-durum-dagilimi/excel`,
            { params, responseType: 'blob' }
        );
    }

    private buildParams(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odaTipiId: number | null,
        durum: string | null
    ): HttpParams {
        let params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangic', baslangic)
            .set('bitis', bitis);

        if (odaTipiId) {
            params = params.set('odaTipiId', odaTipiId);
        }

        if (durum) {
            params = params.set('durum', durum);
        }

        return params;
    }
}
