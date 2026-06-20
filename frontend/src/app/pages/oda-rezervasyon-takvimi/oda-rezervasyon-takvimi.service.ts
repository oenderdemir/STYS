import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { OdaRezervasyonTakvimiDto } from './oda-rezervasyon-takvimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaRezervasyonTakvimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTakvim(
        tesisId: number,
        baslangicTarihi: string,
        gunSayisi: number,
        odaTipiId?: number | null,
        durum?: string | null
    ): Observable<OdaRezervasyonTakvimiDto> {
        let params = new HttpParams()
            .set('tesisId', tesisId)
            .set('baslangicTarihi', baslangicTarihi)
            .set('gunSayisi', gunSayisi);

        if (odaTipiId) {
            params = params.set('odaTipiId', odaTipiId);
        }

        if (durum) {
            params = params.set('durum', durum);
        }

        return this.http
            .get<ApiResponse<OdaRezervasyonTakvimiDto>>(
                `${this.apiBaseUrl}/ui/rezervasyon/oda-rezervasyon-takvimi`,
                { params }
            )
            .pipe(
                map((response) => {
                    if (response.success && response.data) {
                        return response.data;
                    }
                    throw new Error(tryReadApiMessage(response) ?? 'Oda rezervasyon takvimi alınamadı.');
                })
            );
    }
}
