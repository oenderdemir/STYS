import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { AddKantinSatisOdemeRequest, AddKantinSatisSatirRequest, CancelKantinSatisRequest, CreateKantinSatisIadeRequest, CreateKantinSatisRequest, KantinSatisBarkodUrunModel, KantinSatisIadeModel, KantinSatisIadeOzetModel, KantinSatisModel } from './kantin-satis.dto';

@Injectable({ providedIn: 'root' })
export class KantinSatisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId?: number | null, kantinId?: number | null): Observable<KantinSatisModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (kantinId && kantinId > 0) {
            params = params.set('kantinId', kantinId);
        }

        return this.http
            .get<ApiResponse<KantinSatisModel[]>>(`${this.apiBaseUrl}/ui/kantin-satis`, { params })
            .pipe(map(this.unwrap<KantinSatisModel[]>('Kantin satışları alınamadı.')));
    }

    getById(id: number): Observable<KantinSatisModel> {
        return this.http
            .get<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${id}`)
            .pipe(map(this.unwrap<KantinSatisModel>('Kantin satışı alınamadı.')));
    }

    create(request: CreateKantinSatisRequest): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Kantin satışı oluşturulamadı.')));
    }

    addSatir(satisId: number, request: AddKantinSatisSatirRequest): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/satirlar`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Satır eklenemedi.')));
    }

    updateSatir(satisId: number, satirId: number, request: AddKantinSatisSatirRequest): Observable<KantinSatisModel> {
        return this.http
            .put<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/satirlar/${satirId}`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Satır güncellenemedi.')));
    }

    deleteSatir(satisId: number, satirId: number): Observable<void> {
        return this.http
            .delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/satirlar/${satirId}`)
            .pipe(map(this.unwrapVoid('Satır silinemedi.')));
    }

    addOdeme(satisId: number, request: AddKantinSatisOdemeRequest): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/odemeler`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Ödeme eklenemedi.')));
    }

    updateOdeme(satisId: number, odemeId: number, request: AddKantinSatisOdemeRequest): Observable<KantinSatisModel> {
        return this.http
            .put<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/odemeler/${odemeId}`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Ödeme güncellenemedi.')));
    }

    deleteOdeme(satisId: number, odemeId: number): Observable<void> {
        return this.http
            .delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/odemeler/${odemeId}`)
            .pipe(map(this.unwrapVoid('Ödeme silinemedi.')));
    }

    getByBarkod(kantinId: number, barkod: string): Observable<KantinSatisBarkodUrunModel> {
        return this.http
            .get<ApiResponse<KantinSatisBarkodUrunModel>>(`${this.apiBaseUrl}/ui/kantin-satis/kantin/${kantinId}/urun-barkod/${encodeURIComponent(barkod)}`)
            .pipe(map(this.unwrap<KantinSatisBarkodUrunModel>('Ürün barkod ile bulunamadı.')));
    }

    kesinlestir(satisId: number): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/kesinlestir`, {})
            .pipe(map(this.unwrap<KantinSatisModel>('Satış kesinleştirilemedi.')));
    }

    iptal(satisId: number, request: CancelKantinSatisRequest): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/iptal`, request)
            .pipe(map(this.unwrap<KantinSatisModel>('Satış iptal edilemedi.')));
    }

    muhasebeFisiOlustur(satisId: number): Observable<KantinSatisModel> {
        return this.http
            .post<ApiResponse<KantinSatisModel>>(`${this.apiBaseUrl}/ui/kantin-satis/${satisId}/muhasebe-fisi-olustur`, {})
            .pipe(map(this.unwrap<KantinSatisModel>('Muhasebe fişi oluşturulamadı.')));
    }

    getIadeOzeti(kantinSatisId: number): Observable<KantinSatisIadeOzetModel[]> {
        const params = new HttpParams().set('kantinSatisId', kantinSatisId);
        return this.http
            .get<ApiResponse<KantinSatisIadeOzetModel[]>>(`${this.apiBaseUrl}/ui/kantin-satis-iade/ozet`, { params })
            .pipe(map(this.unwrap<KantinSatisIadeOzetModel[]>('İade özeti alınamadı.')));
    }

    createIade(request: CreateKantinSatisIadeRequest): Observable<KantinSatisIadeModel> {
        return this.http
            .post<ApiResponse<KantinSatisIadeModel>>(`${this.apiBaseUrl}/ui/kantin-satis-iade`, request)
            .pipe(map(this.unwrap<KantinSatisIadeModel>('İade oluşturulamadı.')));
    }

    finalizeIade(iadeId: number): Observable<KantinSatisIadeModel> {
        return this.http
            .post<ApiResponse<KantinSatisIadeModel>>(`${this.apiBaseUrl}/ui/kantin-satis-iade/${iadeId}/kesinlestir`, {})
            .pipe(map(this.unwrap<KantinSatisIadeModel>('İade kesinleştirilemedi.')));
    }

    private unwrap<T>(fallback: string) {
        return (envelope: ApiResponse<T>): T => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }

            throw new Error(tryReadApiMessage(envelope) ?? fallback);
        };
    }

    private unwrapVoid(fallback: string) {
        return (envelope: ApiResponse<unknown>): void => {
            if (!envelope.success) {
                throw new Error(tryReadApiMessage(envelope) ?? fallback);
            }
        };
    }
}
