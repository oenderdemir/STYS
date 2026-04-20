import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateKasaBankaHesapRequest, KasaBankaHesapModel, KasaBankaHesapTipi, MuhasebeTesisModel, UpdateKasaBankaHesapRequest } from './kasa-banka-hesaplari.dto';

@Injectable({ providedIn: 'root' })
export class KasaBankaHesaplariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null): Observable<PagedResponseDto<KasaBankaHesapModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        return this.http.get<ApiResponse<PagedResponseDto<KasaBankaHesapModel>>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari/paged`, { params }).pipe(map(this.unwrapOne));
    }

    getByTip(tip: KasaBankaHesapTipi, sadeceAktif = true): Observable<KasaBankaHesapModel[]> {
        const params = new HttpParams().set('sadeceAktif', sadeceAktif);
        return this.http.get<ApiResponse<KasaBankaHesapModel[]>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari/tip/${tip}`, { params }).pipe(map(this.unwrapList));
    }

    getMuhasebeHesapSecimleri(tip: KasaBankaHesapTipi): Observable<Array<{ id: number; tamKod: string; ad: string }>> {
        return this.http.get<ApiResponse<Array<{ id: number; tamKod: string; ad: string }>>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari/muhasebe-hesap-secimleri/${tip}`).pipe(map(this.unwrapOne));
    }

    create(payload: CreateKasaBankaHesapRequest): Observable<KasaBankaHesapModel> {
        return this.http.post<ApiResponse<KasaBankaHesapModel>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari`, payload).pipe(map(this.unwrapOne));
    }

    update(id: number, payload: UpdateKasaBankaHesapRequest): Observable<KasaBankaHesapModel> {
        return this.http.put<ApiResponse<KasaBankaHesapModel>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari/${id}`, payload).pipe(map(this.unwrapOne));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/kasa-banka-hesaplari/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }

            throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
        }));
    }

    getTesisler(): Observable<MuhasebeTesisModel[]> {
        return this.http.get<ApiResponse<MuhasebeTesisModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(map(this.unwrapOne));
    }

    private unwrapList(envelope: ApiResponse<KasaBankaHesapModel[]>): KasaBankaHesapModel[] {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }

        throw new Error(tryReadApiMessage(envelope) ?? 'Liste alinamadi.');
    }

    private unwrapOne<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }

        throw new Error(tryReadApiMessage(envelope) ?? 'Kayit alinamadi.');
    }
}
