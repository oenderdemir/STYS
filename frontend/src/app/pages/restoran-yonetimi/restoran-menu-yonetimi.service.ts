import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    CreateRestoranGlobalMenuKategoriRequest,
    CreateRestoranMenuKategoriRequest,
    CreateRestoranMenuUrunRequest,
    RestoranGlobalMenuKategoriModel,
    RestoranKategoriAtamaBaglamModel,
    RestoranMenuKategoriModel,
    RestoranMenuUrunModel,
    SaveRestoranKategoriAtamaRequest,
    UpdateRestoranGlobalMenuKategoriRequest,
    UpdateRestoranMenuKategoriRequest,
    UpdateRestoranMenuUrunRequest
} from './restoran-yonetimi.dto';

interface RestoranMenuResponse {
    restoranId: number;
    kategoriler: Array<{
        id: number;
        ad: string;
        siraNo: number;
        urunler: RestoranMenuUrunModel[];
    }>;
}

@Injectable({ providedIn: 'root' })
export class RestoranMenuYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getMenuByRestoranId(restoranId: number): Observable<{ kategoriler: RestoranMenuKategoriModel[]; urunMap: Record<number, RestoranMenuUrunModel[]> }> {
        return this.http.get<ApiResponse<RestoranMenuResponse>>(`${this.apiBaseUrl}/api/restoranlar/${restoranId}/menu`).pipe(
            map((responseEnvelope) => {
                if (!responseEnvelope.success || !responseEnvelope.data) {
                    throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran menusu alinamadi.');
                }

                const kategoriler: RestoranMenuKategoriModel[] = responseEnvelope.data.kategoriler.map((kategori) => ({
                    id: kategori.id,
                    restoranId,
                    ad: kategori.ad,
                    siraNo: kategori.siraNo,
                    aktifMi: true
                }));

                const urunMap: Record<number, RestoranMenuUrunModel[]> = {};
                for (const kategori of responseEnvelope.data.kategoriler) {
                    urunMap[kategori.id] = kategori.urunler ?? [];
                }

                return { kategoriler, urunMap };
            })
        );
    }

    createKategori(payload: CreateRestoranMenuKategoriRequest): Observable<RestoranMenuKategoriModel> {
        return this.http.post<ApiResponse<RestoranMenuKategoriModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kategori olusturulamadi.');
            })
        );
    }

    updateKategori(id: number, payload: UpdateRestoranMenuKategoriRequest): Observable<RestoranMenuKategoriModel> {
        return this.http.put<ApiResponse<RestoranMenuKategoriModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kategori guncellenemedi.');
            })
        );
    }

    deleteKategori(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kategori silinemedi.');
            })
        );
    }

    createUrun(payload: CreateRestoranMenuUrunRequest): Observable<RestoranMenuUrunModel> {
        return this.http.post<ApiResponse<RestoranMenuUrunModel>>(`${this.apiBaseUrl}/api/restoran-menu-urunleri`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu urunu olusturulamadi.');
            })
        );
    }

    updateUrun(id: number, payload: UpdateRestoranMenuUrunRequest): Observable<RestoranMenuUrunModel> {
        return this.http.put<ApiResponse<RestoranMenuUrunModel>>(`${this.apiBaseUrl}/api/restoran-menu-urunleri/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu urunu guncellenemedi.');
            })
        );
    }

    deleteUrun(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/restoran-menu-urunleri/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu urunu silinemedi.');
            })
        );
    }

    getGlobalKategoriler(): Observable<RestoranGlobalMenuKategoriModel[]> {
        return this.http.get<ApiResponse<RestoranGlobalMenuKategoriModel[]>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/global`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global kategori listesi alinamadi.');
            })
        );
    }

    createGlobalKategori(payload: CreateRestoranGlobalMenuKategoriRequest): Observable<RestoranGlobalMenuKategoriModel> {
        return this.http.post<ApiResponse<RestoranGlobalMenuKategoriModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/global`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global kategori olusturulamadi.');
            })
        );
    }

    updateGlobalKategori(id: number, payload: UpdateRestoranGlobalMenuKategoriRequest): Observable<RestoranGlobalMenuKategoriModel> {
        return this.http.put<ApiResponse<RestoranGlobalMenuKategoriModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/global/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global kategori guncellenemedi.');
            })
        );
    }

    deleteGlobalKategori(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/global/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Global kategori silinemedi.');
            })
        );
    }

    getKategoriAtamaBaglam(restoranId: number): Observable<RestoranKategoriAtamaBaglamModel> {
        return this.http.get<ApiResponse<RestoranKategoriAtamaBaglamModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/atama-baglam`, { params: { restoranId } }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kategori atama baglami alinamadi.');
            })
        );
    }

    saveKategoriAtamalari(payload: SaveRestoranKategoriAtamaRequest): Observable<RestoranKategoriAtamaBaglamModel> {
        return this.http.put<ApiResponse<RestoranKategoriAtamaBaglamModel>>(`${this.apiBaseUrl}/api/restoran-menu-kategorileri/atamalar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kategori atamalari kaydedilemedi.');
            })
        );
    }
}
