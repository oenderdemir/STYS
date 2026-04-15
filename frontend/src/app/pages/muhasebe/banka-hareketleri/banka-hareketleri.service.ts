import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { BankaHareketModel, CreateBankaHareketRequest, UpdateBankaHareketRequest } from './banka-hareketleri.dto';

@Injectable({ providedIn: 'root' })
export class BankaHareketleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<BankaHareketModel[]> {
        return this.http.get<ApiResponse<BankaHareketModel[]>>(`${this.apiBaseUrl}/api/muhasebe/banka-hareketleri`).pipe(map(this.unwrapList));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<BankaHareketModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<BankaHareketModel>>>(`${this.apiBaseUrl}/api/muhasebe/banka-hareketleri/paged`, { params }).pipe(map(this.unwrapSingle));
    }

    create(payload: CreateBankaHareketRequest): Observable<BankaHareketModel> {
        return this.http.post<ApiResponse<BankaHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/banka-hareketleri`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateBankaHareketRequest): Observable<BankaHareketModel> {
        return this.http.put<ApiResponse<BankaHareketModel>>(`${this.apiBaseUrl}/api/muhasebe/banka-hareketleri/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/banka-hareketleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
        }));
    }

    private unwrapList(envelope: ApiResponse<BankaHareketModel[]>): BankaHareketModel[] {
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

