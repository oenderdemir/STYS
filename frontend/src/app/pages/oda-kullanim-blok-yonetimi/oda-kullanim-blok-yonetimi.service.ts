import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { OdaKullanimBlokDto, OdaKullanimBlokOdaSecenekDto, OdaKullanimBlokTesisDto } from './oda-kullanim-blok-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class OdaKullanimBlokYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(
        pageNumber: number,
        pageSize: number,
        query: string,
        sortBy: string,
        sortDir: SortDirection,
        tesisId: number | null,
        odaId: number | null,
        blokTipi: string | null
    ): Observable<PagedResponseDto<OdaKullanimBlokDto>> {
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

        if (odaId && odaId > 0) {
            params = params.set('odaId', odaId);
        }

        if (blokTipi && blokTipi.trim().length > 0) {
            params = params.set('blokTipi', blokTipi.trim());
        }

        return this.http.get<ApiResponse<PagedResponseDto<OdaKullanimBlokDto>>>(`${this.apiBaseUrl}/ui/odakullanimblok/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda bakim/ariza listesi alinamadi.');
            })
        );
    }

    create(payload: OdaKullanimBlokDto): Observable<OdaKullanimBlokDto> {
        return this.http.post<ApiResponse<OdaKullanimBlokDto>>(`${this.apiBaseUrl}/ui/odakullanimblok`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kayit olusturulamadi.');
            })
        );
    }

    update(id: number, payload: OdaKullanimBlokDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odakullanimblok/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kayit guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/odakullanimblok/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kayit silinemedi.');
            })
        );
    }

    getTesisler(): Observable<OdaKullanimBlokTesisDto[]> {
        return this.http.get<ApiResponse<OdaKullanimBlokTesisDto[]>>(`${this.apiBaseUrl}/ui/odakullanimblok/tesisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    getOdalar(tesisId: number): Observable<OdaKullanimBlokOdaSecenekDto[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<OdaKullanimBlokOdaSecenekDto[]>>(`${this.apiBaseUrl}/ui/odakullanimblok/odalar`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda listesi alinamadi.');
            })
        );
    }
}

