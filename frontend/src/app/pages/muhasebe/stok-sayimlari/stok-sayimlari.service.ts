import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { AddStokSayimSatirRequest, CreateStokSayimRequest, StokSayimModel, UpdateStokSayimSatirlarRequest } from './stok-sayimlari.dto';

@Injectable({ providedIn: 'root' })
export class StokSayimlariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null, depoId?: number | null): Observable<PagedResponseDto<StokSayimModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<StokSayimModel>>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/paged`, { params })
            .pipe(map(this.unwrap<PagedResponseDto<StokSayimModel>>('Stok sayimlari alinamadi.')));
    }

    getById(id: number): Observable<StokSayimModel> {
        return this.http.get<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}`)
            .pipe(map(this.unwrap<StokSayimModel>('Stok sayimi alinamadi.')));
    }

    create(payload: CreateStokSayimRequest): Observable<StokSayimModel> {
        return this.http.post<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari`, payload)
            .pipe(map(this.unwrap<StokSayimModel>('Stok sayimi olusturulamadi.')));
    }

    update(id: number, payload: CreateStokSayimRequest): Observable<StokSayimModel> {
        return this.http.put<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}`, payload)
            .pipe(map(this.unwrap<StokSayimModel>('Stok sayimi guncellenemedi.')));
    }

    updateSatirlar(id: number, payload: UpdateStokSayimSatirlarRequest): Observable<StokSayimModel> {
        return this.http.put<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/satirlar`, payload)
            .pipe(map(this.unwrap<StokSayimModel>('Sayim satirlari kaydedilemedi.')));
    }

    addSatir(id: number, payload: AddStokSayimSatirRequest): Observable<StokSayimModel> {
        return this.http.post<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/satirlar`, payload)
            .pipe(map(this.unwrap<StokSayimModel>('Sayim satiri eklenemedi.')));
    }

    deleteSatir(id: number, satirId: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/satirlar/${satirId}`)
            .pipe(map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Sayim satiri silinemedi.');
            }));
    }

    refresh(id: number): Observable<StokSayimModel> {
        return this.http.post<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/refresh`, {})
            .pipe(map(this.unwrap<StokSayimModel>('Sayim yenilenemedi.')));
    }

    kesinlestir(id: number): Observable<StokSayimModel> {
        return this.http.post<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/kesinlestir`, {})
            .pipe(map(this.unwrap<StokSayimModel>('Sayim kesinlestirilemedi.')));
    }

    iptal(id: number): Observable<StokSayimModel> {
        return this.http.post<ApiResponse<StokSayimModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-sayimlari/${id}/iptal`, {})
            .pipe(map(this.unwrap<StokSayimModel>('Sayim iptal edilemedi.')));
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
