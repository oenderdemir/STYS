import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CurrentStokMaliyetPolitikasiModel, StokMaliyetPolitikasiModel, UpsertStokMaliyetPolitikasiRequest } from '../stok-hareketleri/stok-hareketleri.dto';

@Injectable({ providedIn: 'root' })
export class StokMaliyetPolitikasiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getCurrent(tesisId: number, tarih: string): Observable<CurrentStokMaliyetPolitikasiModel> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('tarih', tarih);

        return this.http
            .get<ApiResponse<CurrentStokMaliyetPolitikasiModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-maliyet-politikalari/current`, { params })
            .pipe(map(this.unwrap<CurrentStokMaliyetPolitikasiModel>('Stok maliyet politikasi alinamadi.')));
    }

    upsert(payload: UpsertStokMaliyetPolitikasiRequest): Observable<StokMaliyetPolitikasiModel> {
        return this.http
            .post<ApiResponse<StokMaliyetPolitikasiModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-maliyet-politikalari`, payload)
            .pipe(map(this.unwrap<StokMaliyetPolitikasiModel>('Stok maliyet politikasi kaydedilemedi.')));
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
