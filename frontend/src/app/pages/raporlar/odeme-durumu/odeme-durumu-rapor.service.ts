import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { OdemeDurumuRaporDto } from './odeme-durumu-rapor.dto';

@Injectable({ providedIn: 'root' })
export class OdemeDurumuRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        baslangic: string,
        bitis: string,
        odemeDurumu: string
    ): Observable<OdemeDurumuRaporDto> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangic', baslangic)
            .set('bitis', bitis)
            .set('odemeDurumu', odemeDurumu);

        return this.http
            .get<ApiResponse<OdemeDurumuRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/odeme-durumu`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Ödeme durumu raporu alınamadı.');
                })
            );
    }

    exportExcel(tesisId: number, baslangic: string, bitis: string, odemeDurumu: string): Observable<Blob> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangic', baslangic)
            .set('bitis', bitis)
            .set('odemeDurumu', odemeDurumu);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/odeme-durumu/excel`,
            { params, responseType: 'blob' }
        );
    }
}
