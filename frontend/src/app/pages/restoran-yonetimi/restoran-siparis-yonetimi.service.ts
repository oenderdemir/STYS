import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    AktifRezervasyonAramaModel,
    CreateKrediKartiOdemeRequest,
    CreateNakitOdemeRequest,
    CreateOdayaEkleOdemeRequest,
    CreateRestoranSiparisRequest,
    RestoranOdemeModel,
    RestoranSiparisModel,
    RestoranSiparisOdemeOzetiModel,
    UpdateRestoranSiparisDurumRequest,
    UpdateRestoranSiparisRequest
} from './restoran-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class RestoranSiparisYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(restoranId?: number | null): Observable<RestoranSiparisModel[]> {
        let params = new HttpParams();
        if (restoranId && restoranId > 0) {
            params = params.set('restoranId', restoranId);
        }

        return this.http.get<ApiResponse<RestoranSiparisModel[]>>(`${this.apiBaseUrl}/api/restoran-siparisleri`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Siparis listesi alinamadi.');
            })
        );
    }

    getById(id: number): Observable<RestoranSiparisModel> {
        return this.http.get<ApiResponse<RestoranSiparisModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Siparis detayi alinamadi.');
            })
        );
    }

    getByRestoranId(restoranId: number): Observable<RestoranSiparisModel[]> {
        return this.http.get<ApiResponse<RestoranSiparisModel[]>>(`${this.apiBaseUrl}/api/restoranlar/${restoranId}/siparisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran siparisleri alinamadi.');
            })
        );
    }

    getAcikByMasaId(masaId?: number | null): Observable<RestoranSiparisModel[]> {
        let params = new HttpParams();
        if (masaId && masaId > 0) {
            params = params.set('masaId', masaId);
        }

        return this.http.get<ApiResponse<RestoranSiparisModel[]>>(`${this.apiBaseUrl}/api/restoran-siparisleri/acik`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Acik siparisler alinamadi.');
            })
        );
    }

    create(payload: CreateRestoranSiparisRequest): Observable<RestoranSiparisModel> {
        return this.http.post<ApiResponse<RestoranSiparisModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Siparis olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateRestoranSiparisRequest): Observable<RestoranSiparisModel> {
        return this.http.put<ApiResponse<RestoranSiparisModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Siparis guncellenemedi.');
            })
        );
    }

    updateDurum(id: number, payload: UpdateRestoranSiparisDurumRequest): Observable<RestoranSiparisModel> {
        return this.http.put<ApiResponse<RestoranSiparisModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${id}/durum`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Siparis durumu guncellenemedi.');
            })
        );
    }

    getOdemeler(siparisId: number): Observable<RestoranOdemeModel[]> {
        return this.http.get<ApiResponse<RestoranOdemeModel[]>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${siparisId}/odemeler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Odeme gecmisi alinamadi.');
            })
        );
    }

    getOdemeOzeti(siparisId: number): Observable<RestoranSiparisOdemeOzetiModel> {
        return this.http.get<ApiResponse<RestoranSiparisOdemeOzetiModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${siparisId}/odeme-ozeti`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Odeme ozeti alinamadi.');
            })
        );
    }

    nakitOdemeAl(siparisId: number, payload: CreateNakitOdemeRequest): Observable<RestoranOdemeModel> {
        return this.http.post<ApiResponse<RestoranOdemeModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${siparisId}/odemeler/nakit`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Nakit odeme alinamadi.');
            })
        );
    }

    krediKartiOdemeAl(siparisId: number, payload: CreateKrediKartiOdemeRequest): Observable<RestoranOdemeModel> {
        return this.http.post<ApiResponse<RestoranOdemeModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${siparisId}/odemeler/kredi-karti`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kredi karti odemesi alinamadi.');
            })
        );
    }

    odayaEkle(siparisId: number, payload: CreateOdayaEkleOdemeRequest): Observable<RestoranOdemeModel> {
        return this.http.post<ApiResponse<RestoranOdemeModel>>(`${this.apiBaseUrl}/api/restoran-siparisleri/${siparisId}/odemeler/odaya-ekle`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Odaya ekleme islemi basarisiz.');
            })
        );
    }

    uygunRezervasyonAra(tesisId: number, term: string): Observable<AktifRezervasyonAramaModel[]> {
        let params = new HttpParams().set('tesisId', tesisId);
        const normalizedTerm = term.trim();
        if (normalizedTerm) {
            params = params.set('q', normalizedTerm);
        }

        return this.http.get<ApiResponse<AktifRezervasyonAramaModel[]>>(`${this.apiBaseUrl}/api/restoran-siparisleri/aktif-rezervasyonlar`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Uygun rezervasyon listesi alinamadi.');
            })
        );
    }
}
