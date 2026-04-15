import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CariBakiyeModel, CariKartModel, CreateCariKartRequest, UpdateCariKartRequest } from './cari-kartlar.dto';

@Injectable({ providedIn: 'root' })
export class CariKartlarService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<CariKartModel[]> {
        return this.http.get<ApiResponse<CariKartModel[]>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kartlar alinamadi.');
            })
        );
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<CariKartModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<CariKartModel>>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar/paged`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kartlar alinamadi.');
            })
        );
    }

    getById(id: number): Observable<CariKartModel> {
        return this.http.get<ApiResponse<CariKartModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar/${id}`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kart detayi alinamadi.');
            })
        );
    }

    create(payload: CreateCariKartRequest): Observable<CariKartModel> {
        return this.http.post<ApiResponse<CariKartModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar`, payload).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kart olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateCariKartRequest): Observable<CariKartModel> {
        return this.http.put<ApiResponse<CariKartModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar/${id}`, payload).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kart guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar/${id}`).pipe(
            map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari kart silinemedi.');
            })
        );
    }

    getBakiye(id: number): Observable<CariBakiyeModel> {
        return this.http.get<ApiResponse<CariBakiyeModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-kartlar/${id}/bakiye`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Cari bakiye alinamadi.');
            })
        );
    }
}

