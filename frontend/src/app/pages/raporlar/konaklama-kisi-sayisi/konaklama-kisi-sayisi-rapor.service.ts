import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { KonaklamaKisiSayisiRaporDto } from './konaklama-kisi-sayisi-rapor.dto';

@Injectable({ providedIn: 'root' })
export class KonaklamaKisiSayisiRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(
        tesisId: number,
        ay: number,
        baslangicYil: number,
        bitisYil: number
    ): Observable<KonaklamaKisiSayisiRaporDto> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('ay', ay)
            .set('baslangicYil', baslangicYil)
            .set('bitisYil', bitisYil);

        return this.http
            .get<ApiResponse<KonaklamaKisiSayisiRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/konaklama-kisi-sayisi`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Konaklama kişi sayısı raporu alınamadı.');
                })
            );
    }

    exportExcel(tesisId: number, ay: number, baslangicYil: number, bitisYil: number): Observable<Blob> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('ay', ay)
            .set('baslangicYil', baslangicYil)
            .set('bitisYil', bitisYil);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/konaklama-kisi-sayisi/excel`,
            { params, responseType: 'blob' }
        );
    }
}
