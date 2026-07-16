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
        return this.http.get<ApiResponse<TahsilatOdemeBelgesiModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri`).pipe(map(this.unwrapList));
    }

    getPaged(pageNumber: number, pageSize: number, tesisId: number): Observable<PagedResponseDto<TahsilatOdemeBelgesiModel>> {
        const params = new HttpParams()
            .set('pageNumber', pageNumber)
            .set('pageSize', pageSize)
            .set('tesisId', tesisId);
        return this.http.get<ApiResponse<PagedResponseDto<TahsilatOdemeBelgesiModel>>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/paged`, { params }).pipe(map(this.unwrapSingle));
    }

    create(payload: CreateTahsilatOdemeBelgesiRequest): Observable<TahsilatOdemeBelgesiModel> {
        return this.http.post<ApiResponse<TahsilatOdemeBelgesiModel>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri`, payload).pipe(map(this.unwrapSingle));
    }

    update(id: number, payload: UpdateTahsilatOdemeBelgesiRequest): Observable<TahsilatOdemeBelgesiModel> {
        return this.http.put<ApiResponse<TahsilatOdemeBelgesiModel>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/${id}`, payload).pipe(map(this.unwrapSingle));
    }

    delete(id: number): Observable<void> {
        return this.iptalEt(id);
    }

    iptalEt(id: number): Observable<void> {
        return this.http.post<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/${id}/iptal`, {}).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Belge iptal edilemedi.');
        }));
    }

    kapamaGeriAl(id: number): Observable<void> {
        return this.http.post<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/${id}/kapama-geri-al`, {}).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Kapama geri alinamadi.');
        }));
    }

    /** Ayri, bilincli bir aksiyondur — belge olusurken otomatik cagrilmaz. */
    muhasebeFisiOlustur(id: number): Observable<TahsilatOdemeBelgesiModel> {
        return this.http.post<ApiResponse<TahsilatOdemeBelgesiModel>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/${id}/muhasebe-fisi-olustur`, {}).pipe(map((envelope) => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe fisi olusturulamadi.');
        }));
    }

    getGunlukOzet(gun?: string | null, tesisId?: number | null): Observable<TahsilatOdemeOzetModel> {
        let params = new HttpParams();
        if (gun) {
            params = params.set('gun', gun);
        }
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<TahsilatOdemeOzetModel>>(`${this.apiBaseUrl}/ui/muhasebe/tahsilat-odeme-belgeleri/gunluk-ozet`, { params }).pipe(map(this.unwrapSingle));
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

