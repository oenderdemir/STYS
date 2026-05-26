import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CariBakiyeOzetModel, CariEkstreModel, CariHareketDurumOzetModel, CariHareketModel, CreateCariHareketRequest, UpdateCariHareketRequest } from './cari-hareketler.dto';

@Injectable({ providedIn: 'root' })
export class CariHareketlerService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId?: number | null, cariKartId?: number | null): Observable<CariHareketModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (cariKartId && cariKartId > 0) {
            params = params.set('cariKartId', cariKartId);
        }

        return this.http.get<ApiResponse<CariHareketModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler`, { params }).pipe(map((envelope) => this.unwrapList(envelope)));
    }

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null, cariKartId?: number | null): Observable<PagedResponseDto<CariHareketModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (cariKartId && cariKartId > 0) {
            params = params.set('cariKartId', cariKartId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<CariHareketModel>>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/paged`, { params }).pipe(map((envelope) => this.unwrapSingle(envelope)));
    }

    create(payload: CreateCariHareketRequest): Observable<CariHareketModel> {
        return this.http.post<ApiResponse<CariHareketModel>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateCariHareketRequest): Observable<CariHareketModel> {
        return this.http.put<ApiResponse<CariHareketModel>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/${id}`).pipe(
            map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
            })
        );
    }

    getEkstre(cariKartId: number, baslangic?: string | null, bitis?: string | null): Observable<CariEkstreModel> {
        let params = new HttpParams();
        if (baslangic) {
            params = params.set('baslangic', baslangic);
        }
        if (bitis) {
            params = params.set('bitis', bitis);
        }

        return this.http.get<ApiResponse<CariEkstreModel>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/cari/${cariKartId}/ekstre`, { params }).pipe(map((envelope) => this.unwrapSingle(envelope)));
    }

    getBakiyeOzet(cariKartId: number): Observable<CariBakiyeOzetModel> {
        return this.http.get<ApiResponse<CariBakiyeOzetModel>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/cari/${cariKartId}/bakiye-ozet`).pipe(map((envelope) => this.unwrapSingle(envelope)));
    }

    getAcikHareketler(cariKartId: number): Observable<CariHareketDurumOzetModel[]> {
        return this.http.get<ApiResponse<CariHareketDurumOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/cari/${cariKartId}/acik-hareketler`).pipe(map((envelope) => this.unwrapList(envelope)));
    }

    getKapananHareketler(cariKartId: number): Observable<CariHareketDurumOzetModel[]> {
        return this.http.get<ApiResponse<CariHareketDurumOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/cari/${cariKartId}/kapanan-hareketler`).pipe(map((envelope) => this.unwrapList(envelope)));
    }

    getCariHareketEkstre(cariKartId: number, baslangic?: Date | null, bitis?: Date | null): Observable<CariHareketDurumOzetModel[]> {
        let params = new HttpParams();
        if (baslangic) {
            params = params.set('baslangic', baslangic.toISOString());
        }
        if (bitis) {
            params = params.set('bitis', bitis.toISOString());
        }

        return this.http.get<ApiResponse<CariHareketDurumOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/cari-hareketler/cari/${cariKartId}/hareket-ekstre`, { params }).pipe(map((envelope) => this.unwrapList(envelope)));
    }

    getHareketEkstre(cariKartId: number, baslangic?: Date | null, bitis?: Date | null): Observable<CariHareketDurumOzetModel[]> {
        return this.getCariHareketEkstre(cariKartId, baslangic, bitis);
    }

    private unwrapList<T>(envelope: ApiResponse<T[]>): T[] {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'Liste alinamadi.');
    }

    private unwrapSingle<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'Kayit alinamadi.');
    }
}

