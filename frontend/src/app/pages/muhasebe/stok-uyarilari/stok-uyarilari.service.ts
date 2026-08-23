import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { StokUyariModel } from './stok-uyarilari.dto';

@Injectable({ providedIn: 'root' })
export class StokUyarilariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId: number, depoId?: number | null, tasinirKartId?: number | null, sadeceRiskli = false): Observable<StokUyariModel[]> {
        let params = new HttpParams().set('tesisId', tesisId);
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }
        if (tasinirKartId && tasinirKartId > 0) {
            params = params.set('tasinirKartId', tasinirKartId);
        }
        if (sadeceRiskli) {
            params = params.set('sadeceRiskli', true);
        }

        return this.http
            .get<ApiResponse<StokUyariModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/stok-uyarilari`, { params })
            .pipe(map(this.unwrap<StokUyariModel[]>('Stok uyarıları alınamadı.')));
    }

    private unwrap<T>(fallback: string) {
        return (envelope: ApiResponse<T>): T => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }

            throw new Error(tryReadApiMessage(envelope) ?? fallback);
        };
    }
}

