import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateKasaHareketRequest, KasaHareketModel, UpdateKasaHareketRequest } from './kasa-hareketleri.dto';

@Injectable({ providedIn: 'root' })
export class KasaHareketleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<KasaHareketModel[]> {
        return this.http.get<ApiResponse<KasaHareketModel[]>>(`${this.apiBaseUrl}/api/muhasebe/kasa-hareketleri`).pipe(map(this.unwrapList));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<KasaHareketModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<KasaHareketModel>>>(`${this.apiBaseUrl}/api/muhasebe/kasa-hareketleri/paged`, { params }).pipe(map(this.unwrapSingle));
    }

    create(payload: CreateKasaHareketRequest): Observable<KasaHareketModel> {
        return this.http.post<ApiResponse<KasaHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/kasa-hareketleri`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateKasaHareketRequest): Observable<KasaHareketModel> {
        return this.http.put<ApiResponse<KasaHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/kasa-hareketleri/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/kasa-hareketleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
        }));
    }

    private unwrapList(envelope: ApiResponse<KasaHareketModel[]>): KasaHareketModel[] {
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

