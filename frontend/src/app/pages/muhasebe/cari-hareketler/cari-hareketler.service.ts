import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CariEkstreModel, CariHareketModel, CreateCariHareketRequest, UpdateCariHareketRequest } from './cari-hareketler.dto';

@Injectable({ providedIn: 'root' })
export class CariHareketlerService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(cariKartId?: number | null): Observable<CariHareketModel[]> {
        let params = new HttpParams();
        if (cariKartId && cariKartId > 0) {
            params = params.set('cariKartId', cariKartId);
        }

        return this.http.get<ApiResponse<CariHareketModel[]>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler`, { params }).pipe(map(this.unwrapList));
    }

    getPaged(pageNumber: number, pageSize: number, cariKartId?: number | null): Observable<PagedResponseDto<CariHareketModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (cariKartId && cariKartId > 0) {
            params = params.set('cariKartId', cariKartId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<CariHareketModel>>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler/paged`, { params }).pipe(map(this.unwrapSingle));
    }

    create(payload: CreateCariHareketRequest): Observable<CariHareketModel> {
        return this.http.post<ApiResponse<CariHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateCariHareketRequest): Observable<CariHareketModel> {
        return this.http.put<ApiResponse<CariHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler/${id}`).pipe(
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

        return this.http.get<ApiResponse<CariEkstreModel>>(`${this.apiBaseUrl}/api/muhasebe/cari-hareketler/cari/${cariKartId}/ekstre`, { params }).pipe(map(this.unwrapSingle));
    }

    private unwrapList(envelope: ApiResponse<CariHareketModel[]>): CariHareketModel[] {
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

