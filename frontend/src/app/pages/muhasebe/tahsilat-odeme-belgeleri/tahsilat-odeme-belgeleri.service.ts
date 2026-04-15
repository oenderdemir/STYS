import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateTahsilatOdemeBelgesiRequest, TahsilatOdemeBelgesiModel, TahsilatOdemeOzetModel, UpdateTahsilatOdemeBelgesiRequest } from './tahsilat-odeme-belgeleri.dto';

@Injectable({ providedIn: 'root' })
export class TahsilatOdemeBelgeleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<TahsilatOdemeBelgesiModel[]> {
        return this.http.get<ApiResponse<TahsilatOdemeBelgesiModel[]>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri`).pipe(map(this.unwrapList));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<TahsilatOdemeBelgesiModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<TahsilatOdemeBelgesiModel>>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri/paged`, { params }).pipe(map(this.unwrapSingle));
    }

    create(payload: CreateTahsilatOdemeBelgesiRequest): Observable<TahsilatOdemeBelgesiModel> {
        return this.http.post<ApiResponse<TahsilatOdemeBelgesiModel>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateTahsilatOdemeBelgesiRequest): Observable<TahsilatOdemeBelgesiModel> {
        return this.http.put<ApiResponse<TahsilatOdemeBelgesiModel>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Kayit silinemedi.');
        }));
    }

    getGunlukOzet(gun?: string | null): Observable<TahsilatOdemeOzetModel> {
        let params = new HttpParams();
        if (gun) {
            params = params.set('gun', gun);
        }

        return this.http.get<ApiResponse<TahsilatOdemeOzetModel>>(`${this.apiBaseUrl}/api/muhasebe/tahsilat-odeme-belgeleri/gunluk-ozet`, { params }).pipe(map(this.unwrapSingle));
    }

    private unwrapList(envelope: ApiResponse<TahsilatOdemeBelgesiModel[]>): TahsilatOdemeBelgesiModel[] {
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

