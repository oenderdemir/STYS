import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateKonaklamaVergisiHesapEslemeRequest,
    KonaklamaVergisiHesapEslemeFilterDto,
    KonaklamaVergisiHesapEslemeModel,
    KonaklamaVergisiHesapSecenekModel,
    UpdateKonaklamaVergisiHesapEslemeRequest
} from './konaklama-vergisi-hesap-eslemeleri.dto';

@Injectable({ providedIn: 'root' })
export class KonaklamaVergisiHesapEslemeleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number, filter: KonaklamaVergisiHesapEslemeFilterDto): Observable<PagedResponseDto<KonaklamaVergisiHesapEslemeModel>> {
        const params = this.toParams(filter)
            .set('pageNumber', pageNumber)
            .set('pageSize', pageSize);

        return this.http.get<ApiResponse<PagedResponseDto<KonaklamaVergisiHesapEslemeModel>>>(
            `${this.apiBaseUrl}/ui/muhasebe/konaklama-vergisi-hesap-eslemeleri/paged`,
            { params }
        ).pipe(map(this.unwrap<PagedResponseDto<KonaklamaVergisiHesapEslemeModel>>('Konaklama vergisi hesap eslemeleri alinamadi.')));
    }

    getHesapSecenekleri(tesisId?: number | null): Observable<KonaklamaVergisiHesapSecenekModel[]> {
        let params = new HttpParams();
        if (tesisId !== null && tesisId !== undefined) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<KonaklamaVergisiHesapSecenekModel[]>>(
            `${this.apiBaseUrl}/ui/muhasebe/konaklama-vergisi-hesap-eslemeleri/hesap-secenekleri`,
            { params }
        ).pipe(map(this.unwrap<KonaklamaVergisiHesapSecenekModel[]>('Konaklama vergisi hesap secenekleri alinamadi.')));
    }

    create(payload: CreateKonaklamaVergisiHesapEslemeRequest): Observable<KonaklamaVergisiHesapEslemeModel> {
        return this.http.post<ApiResponse<KonaklamaVergisiHesapEslemeModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/konaklama-vergisi-hesap-eslemeleri`,
            payload
        ).pipe(map(this.unwrap<KonaklamaVergisiHesapEslemeModel>('Konaklama vergisi hesap esleme olusturulamadi.')));
    }

    update(id: number, payload: UpdateKonaklamaVergisiHesapEslemeRequest): Observable<KonaklamaVergisiHesapEslemeModel> {
        return this.http.put<ApiResponse<KonaklamaVergisiHesapEslemeModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/konaklama-vergisi-hesap-eslemeleri/${id}`,
            payload
        ).pipe(map(this.unwrap<KonaklamaVergisiHesapEslemeModel>('Konaklama vergisi hesap esleme guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/konaklama-vergisi-hesap-eslemeleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }

            throw new Error(tryReadApiMessage(envelope) ?? 'Konaklama vergisi hesap esleme silinemedi.');
        }));
    }

    private toParams(filter: KonaklamaVergisiHesapEslemeFilterDto): HttpParams {
        let params = new HttpParams();
        if (filter.tesisId !== null && filter.tesisId !== undefined) {
            params = params.set('tesisId', filter.tesisId);
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

