import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateTevkifatHesapEslemeRequest,
    TevkifatHesapEslemeFilterDto,
    TevkifatHesapEslemeModel,
    UpdateTevkifatHesapEslemeRequest
} from './tevkifat-hesap-eslemeleri.dto';

@Injectable({ providedIn: 'root' })
export class TevkifatHesapEslemeleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(filter: TevkifatHesapEslemeFilterDto): Observable<TevkifatHesapEslemeModel[]> {
        return this.http.get<ApiResponse<TevkifatHesapEslemeModel[]>>(
            `${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri`,
            { params: this.toParams(filter) }
        ).pipe(map(this.unwrap<TevkifatHesapEslemeModel[]>('Tevkifat hesap eslemeleri alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number, filter: TevkifatHesapEslemeFilterDto): Observable<PagedResponseDto<TevkifatHesapEslemeModel>> {
        const params = this.toParams(filter)
            .set('pageNumber', pageNumber)
            .set('pageSize', pageSize);

        return this.http.get<ApiResponse<PagedResponseDto<TevkifatHesapEslemeModel>>>(
            `${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri/paged`,
            { params }
        ).pipe(map(this.unwrap<PagedResponseDto<TevkifatHesapEslemeModel>>('Tevkifat hesap eslemeleri alinamadi.')));
    }

    getById(id: number): Observable<TevkifatHesapEslemeModel> {
        return this.http.get<ApiResponse<TevkifatHesapEslemeModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri/${id}`
        ).pipe(map(this.unwrap<TevkifatHesapEslemeModel>('Tevkifat hesap esleme alinamadi.')));
    }

    create(payload: CreateTevkifatHesapEslemeRequest): Observable<TevkifatHesapEslemeModel> {
        return this.http.post<ApiResponse<TevkifatHesapEslemeModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri`,
            payload
        ).pipe(map(this.unwrap<TevkifatHesapEslemeModel>('Tevkifat hesap esleme olusturulamadi.')));
    }

    update(id: number, payload: UpdateTevkifatHesapEslemeRequest): Observable<TevkifatHesapEslemeModel> {
        return this.http.put<ApiResponse<TevkifatHesapEslemeModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri/${id}`,
            payload
        ).pipe(map(this.unwrap<TevkifatHesapEslemeModel>('Tevkifat hesap esleme guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/tevkifat-hesap-eslemeleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }

            throw new Error(tryReadApiMessage(envelope) ?? 'Tevkifat hesap esleme silinemedi.');
        }));
    }

    private toParams(filter: TevkifatHesapEslemeFilterDto): HttpParams {
        let params = new HttpParams();
        if (filter.tesisId !== null && filter.tesisId !== undefined) {
            params = params.set('tesisId', filter.tesisId);
        }
        if (filter.islemYonu) {
            params = params.set('islemYonu', filter.islemYonu);
        }
        if (filter.aktifMi !== null && filter.aktifMi !== undefined) {
            params = params.set('aktifMi', filter.aktifMi);
        }
        return params;
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
