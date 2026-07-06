import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { OdaMusaitlikRaporDto } from './oda-musaitlik-rapor.dto';

@Injectable({ providedIn: 'root' })
export class OdaMusaitlikRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        baslangic: string,
        bitis: string,
        durum: string,
        odaTipiId: number | null,
        kapasite: number | null
    ): Observable<OdaMusaitlikRaporDto> {
        const params = this.buildParams(tesisId, baslangic, bitis, durum, odaTipiId, kapasite);

        return this.http
            .get<ApiResponse<OdaMusaitlikRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/oda-musaitlik`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Boş oda / müsaitlik raporu alınamadı.');
                })
            );
    }

    exportExcel(
        tesisId: number,
        baslangic: string,
        bitis: string,
        durum: string,
        odaTipiId: number | null,
        kapasite: number | null
    ): Observable<Blob> {
        const params = this.buildParams(tesisId, baslangic, bitis, durum, odaTipiId, kapasite);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/oda-musaitlik/excel`,
            { params, responseType: 'blob' }
        );
    }

    private buildParams(
        tesisId: number,
        baslangic: string,
        bitis: string,
        durum: string,
        odaTipiId: number | null,
        kapasite: number | null
    ): HttpParams {
        let params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangic', baslangic)
            .set('bitis', bitis)
            .set('durum', durum);

        if (odaTipiId) {
            params = params.set('odaTipiId', odaTipiId);
        }

        if (kapasite) {
            params = params.set('kapasite', kapasite);
        }

        return params;
    }
}
