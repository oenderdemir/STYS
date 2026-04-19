import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateHesapRequest, HesapLookupModel, HesapModel, UpdateHesapRequest } from './hesaplar.dto';

@Injectable({ providedIn: 'root' })
export class HesaplarService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<HesapModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<HesapModel>>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/paged`, { params }).pipe(map(this.unwrapOne));
    }

    getById(id: number): Observable<HesapModel> {
        return this.http.get<ApiResponse<HesapModel>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/${id}`).pipe(map(this.unwrapOne));
    }

    create(payload: CreateHesapRequest): Observable<HesapModel> {
        return this.http.post<ApiResponse<HesapModel>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar`, payload).pipe(map(this.unwrapOne));
    }

    update(id: number, payload: UpdateHesapRequest): Observable<HesapModel> {
        return this.http.put<ApiResponse<HesapModel>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/${id}`, payload).pipe(map(this.unwrapOne));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }

            throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
        }));
    }

    getKasaHesaplari(): Observable<HesapLookupModel[]> {
        return this.http.get<ApiResponse<HesapLookupModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/lookups/kasa-hesaplari`).pipe(map(this.unwrapOne));
    }

    getBankaHesaplari(): Observable<HesapLookupModel[]> {
        return this.http.get<ApiResponse<HesapLookupModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/lookups/banka-hesaplari`).pipe(map(this.unwrapOne));
    }

    getDepolar(): Observable<HesapLookupModel[]> {
        return this.http.get<ApiResponse<HesapLookupModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/lookups/depolar`).pipe(map(this.unwrapOne));
    }

    getMuhasebeKodlari(startsWith = ''): Observable<HesapLookupModel[]> {
        const params = startsWith ? new HttpParams().set('startsWith', startsWith) : undefined;
        return this.http.get<ApiResponse<HesapLookupModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesaplar/lookups/muhasebe-kodlari`, { params }).pipe(map(this.unwrapOne));
    }

    private unwrapOne<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }

        throw new Error(tryReadApiMessage(envelope) ?? 'Veri alinamadi.');
    }
}
