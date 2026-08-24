import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { AddStokTalepSatirRequest, CreateStokTalepRequest, OnayMiktarlariniGuncelleRequest, StokTalepModel, TeslimEtStokTalepRequest, UpdateTalepSatirlariRequest } from './stok-talepleri.dto';

@Injectable({ providedIn: 'root' })
export class StokTalepleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null, talepEdenDepoId?: number | null, karsilayanDepoId?: number | null): Observable<PagedResponseDto<StokTalepModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (talepEdenDepoId && talepEdenDepoId > 0) {
            params = params.set('talepEdenDepoId', talepEdenDepoId);
        }
        if (karsilayanDepoId && karsilayanDepoId > 0) {
            params = params.set('karsilayanDepoId', karsilayanDepoId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<StokTalepModel>>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/paged`, { params })
            .pipe(map(this.unwrap<PagedResponseDto<StokTalepModel>>('Stok talepleri alinamadi.')));
    }

    getById(id: number): Observable<StokTalepModel> {
        return this.http.get<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}`)
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi alinamadi.')));
    }

    create(payload: CreateStokTalepRequest): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi olusturulamadi.')));
    }

    update(id: number, payload: CreateStokTalepRequest): Observable<StokTalepModel> {
        return this.http.put<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi guncellenemedi.')));
    }

    updateTalepSatirlari(id: number, payload: UpdateTalepSatirlariRequest): Observable<StokTalepModel> {
        return this.http.put<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/talep-satirlari`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Talep satirlari kaydedilemedi.')));
    }

    onayMiktarlariniGuncelle(id: number, payload: OnayMiktarlariniGuncelleRequest): Observable<StokTalepModel> {
        return this.http.put<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/onay-miktarlari`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Onay miktarlari kaydedilemedi.')));
    }

    addSatir(id: number, payload: AddStokTalepSatirRequest): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/satirlar`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Talep satiri eklenemedi.')));
    }

    deleteSatir(id: number, satirId: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/satirlar/${satirId}`)
            .pipe(map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Talep satiri silinemedi.');
            }));
    }

    gonder(id: number): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/gonder`, {})
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi gonderilemedi.')));
    }

    reddet(id: number): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/reddet`, {})
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi reddedilemedi.')));
    }

    teslimEt(id: number, payload: TeslimEtStokTalepRequest): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/teslim-et`, payload)
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi teslim edilemedi.')));
    }

    iptal(id: number): Observable<StokTalepModel> {
        return this.http.post<ApiResponse<StokTalepModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}/iptal`, {})
            .pipe(map(this.unwrap<StokTalepModel>('Stok talebi iptal edilemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/stok-talepleri/${id}`)
            .pipe(map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Stok talebi silinemedi.');
            }));
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
