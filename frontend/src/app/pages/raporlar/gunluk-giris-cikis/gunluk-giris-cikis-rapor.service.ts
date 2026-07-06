import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { GunlukGirisCikisRaporDto } from './gunluk-giris-cikis-rapor.dto';

@Injectable({ providedIn: 'root' })
export class GunlukGirisCikisRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRapor(tesisId: number, tarih: string, listeTipi: string): Observable<GunlukGirisCikisRaporDto> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('tarih', tarih)
            .set('listeTipi', listeTipi);

        return this.http
            .get<ApiResponse<GunlukGirisCikisRaporDto>>(
                `${this.apiBaseUrl}/api/raporlar/gunluk-giris-cikis`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Günlük giriş-çıkış listesi alınamadı.');
                })
            );
    }

    exportExcel(tesisId: number, tarih: string, listeTipi: string): Observable<Blob> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('tarih', tarih)
            .set('listeTipi', listeTipi);

        return this.http.get(
            `${this.apiBaseUrl}/api/raporlar/gunluk-giris-cikis/excel`,
            { params, responseType: 'blob' }
        );
    }
}
