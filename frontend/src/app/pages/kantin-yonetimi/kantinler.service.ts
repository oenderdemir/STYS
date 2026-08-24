import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { KantinDepoOption, KantinKasaOption, KantinModel, KantinOdemeHesapOption, KantinTasinirKartOption, KantinUrunModel } from './kantinler.dto';

@Injectable({ providedIn: 'root' })
export class KantinlerService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId?: number | null): Observable<KantinModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http
            .get<ApiResponse<KantinModel[]>>(`${this.apiBaseUrl}/ui/kantinler`, { params })
            .pipe(map(this.unwrap<KantinModel[]>('Kantinler alınamadı.')));
    }

    create(request: KantinModel): Observable<KantinModel> {
        return this.http
            .post<ApiResponse<KantinModel>>(`${this.apiBaseUrl}/ui/kantinler`, request)
            .pipe(map(this.unwrap<KantinModel>('Kantin oluşturulamadı.')));
    }

    update(id: number, request: KantinModel): Observable<KantinModel> {
        return this.http
            .put<ApiResponse<KantinModel>>(`${this.apiBaseUrl}/ui/kantinler/${id}`, request)
            .pipe(map(this.unwrap<KantinModel>('Kantin güncellenemedi.')));
    }

    getUrunler(kantinId: number): Observable<KantinUrunModel[]> {
        return this.http
            .get<ApiResponse<KantinUrunModel[]>>(`${this.apiBaseUrl}/ui/kantinler/${kantinId}/urunler`)
            .pipe(map(this.unwrap<KantinUrunModel[]>('Kantin ürünleri alınamadı.')));
    }

    createUrun(kantinId: number, request: KantinUrunModel): Observable<KantinUrunModel> {
        return this.http
            .post<ApiResponse<KantinUrunModel>>(`${this.apiBaseUrl}/ui/kantinler/${kantinId}/urunler`, request)
            .pipe(map(this.unwrap<KantinUrunModel>('Kantin ürünü oluşturulamadı.')));
    }

    updateUrun(kantinId: number, urunId: number, request: KantinUrunModel): Observable<KantinUrunModel> {
        return this.http
            .put<ApiResponse<KantinUrunModel>>(`${this.apiBaseUrl}/ui/kantinler/${kantinId}/urunler/${urunId}`, request)
            .pipe(map(this.unwrap<KantinUrunModel>('Kantin ürünü güncellenemedi.')));
    }

    getDepolar(tesisId: number): Observable<KantinDepoOption[]> {
        return this.http
            .get<ApiResponse<KantinDepoOption[]>>(`${this.apiBaseUrl}/ui/kantinler/depolar`, { params: new HttpParams().set('tesisId', tesisId) })
            .pipe(map(this.unwrap<KantinDepoOption[]>('Depolar alınamadı.')));
    }

    getNakitKasalar(tesisId: number): Observable<KantinKasaOption[]> {
        return this.http
            .get<ApiResponse<KantinKasaOption[]>>(`${this.apiBaseUrl}/ui/kantinler/nakit-kasalar`, { params: new HttpParams().set('tesisId', tesisId) })
            .pipe(map(this.unwrap<KantinKasaOption[]>('Kasalar alınamadı.')));
    }

    getOdemeHesaplari(tesisId: number, odemeYontemi: string): Observable<KantinOdemeHesapOption[]> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('odemeYontemi', odemeYontemi);

        return this.http
            .get<ApiResponse<KantinOdemeHesapOption[]>>(`${this.apiBaseUrl}/ui/kantinler/odeme-hesaplari`, { params })
            .pipe(map(this.unwrap<KantinOdemeHesapOption[]>('Ödeme hesapları alınamadı.')));
    }

    getTasinirKartlar(tesisId: number): Observable<KantinTasinirKartOption[]> {
        return this.http
            .get<ApiResponse<KantinTasinirKartOption[]>>(`${this.apiBaseUrl}/ui/kantinler/tasinir-kartlar`, { params: new HttpParams().set('tesisId', tesisId) })
            .pipe(map(this.unwrap<KantinTasinirKartOption[]>('Taşınır kartlar alınamadı.')));
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
