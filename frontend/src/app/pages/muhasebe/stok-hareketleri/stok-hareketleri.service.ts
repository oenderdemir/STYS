import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateStokHareketRequest, StokBakiyeModel, StokHareketModel, StokKartOzetModel, UpdateStokHareketRequest } from './stok-hareketleri.dto';

@Injectable({ providedIn: 'root' })
export class StokHareketleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId?: number, depoId?: number): Observable<StokHareketModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<StokHareketModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri`, { params }).pipe(map(this.unwrap<StokHareketModel[]>('Stok hareketleri alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number, tesisId?: number, depoId?: number): Observable<PagedResponseDto<StokHareketModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<StokHareketModel>>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri/paged`, { params }).pipe(map(this.unwrap<PagedResponseDto<StokHareketModel>>('Stok hareketleri alinamadi.')));
    }

    create(payload: CreateStokHareketRequest): Observable<StokHareketModel> {
        return this.http.post<ApiResponse<StokHareketModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri`, payload).pipe(map(this.unwrap<StokHareketModel>('Stok hareket olusturulamadi.')));
    }

    update(id: number, payload: UpdateStokHareketRequest): Observable<StokHareketModel> {
        return this.http.put<ApiResponse<StokHareketModel>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri/${id}`, payload).pipe(map(this.unwrap<StokHareketModel>('Stok hareket guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Stok hareket silinemedi.');
        }));
    }

    getStokBakiye(tesisId?: number, depoId?: number): Observable<StokBakiyeModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<StokBakiyeModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri/stok-bakiye`, { params }).pipe(map(this.unwrap<StokBakiyeModel[]>('Stok bakiye alinamadi.')));
    }

    getStokKartOzet(tesisId?: number, depoId?: number): Observable<StokKartOzetModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<StokKartOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/stok-hareketleri/stok-kart-ozet`, { params }).pipe(map(this.unwrap<StokKartOzetModel[]>('Stok kart ozeti alinamadi.')));
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
