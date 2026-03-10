import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { OdaTemizlikKayitDto, OdaTemizlikTesisDto } from './oda-temizlik-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaTemizlikYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTesisler(): Observable<OdaTemizlikTesisDto[]> {
        return this.http.get<ApiResponse<OdaTemizlikTesisDto[]>>(`${this.apiBaseUrl}/ui/odatemizlik/tesisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    getPaged(
        pageNumber: number,
        pageSize: number,
        query: string,
        sortBy: string,
        sortDir: SortDirection,
        tesisId: number | null,
        durum: string | null
    ): Observable<PagedResponseDto<OdaTemizlikKayitDto>> {
        let params = new HttpParams()
            .set('pageNumber', pageNumber)
            .set('pageSize', pageSize)
            .set('sortBy', sortBy)
            .set('sortDir', sortDir);

        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        if (durum && durum.trim().length > 0) {
            params = params.set('durum', durum.trim());
        }

        return this.http.get<ApiResponse<PagedResponseDto<OdaTemizlikKayitDto>>>(`${this.apiBaseUrl}/ui/odatemizlik/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda temizlik listesi alinamadi.');
            })
        );
    }

    baslatTemizlik(odaId: number): Observable<OdaTemizlikKayitDto> {
        return this.http.post<ApiResponse<OdaTemizlikKayitDto>>(`${this.apiBaseUrl}/ui/odatemizlik/${odaId}/baslat`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Temizlik baslatilamadi.');
            })
        );
    }

    tamamlaTemizlik(odaId: number): Observable<OdaTemizlikKayitDto> {
        return this.http.post<ApiResponse<OdaTemizlikKayitDto>>(`${this.apiBaseUrl}/ui/odatemizlik/${odaId}/tamamla`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Temizlik tamamlanamadi.');
            })
        );
    }
}
