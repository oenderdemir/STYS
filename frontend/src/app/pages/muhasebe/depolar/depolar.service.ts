import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateDepoRequest, DepoModel, MuhasebeTesisModel, UpdateDepoRequest } from './depolar.dto';

@Injectable({ providedIn: 'root' })
export class DepolarService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<DepoModel[]> {
        return this.http.get<ApiResponse<DepoModel[]>>(`${this.apiBaseUrl}/api/muhasebe/depolar`).pipe(map(this.unwrap<DepoModel[]>('Depolar alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null): Observable<PagedResponseDto<DepoModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        return this.http.get<ApiResponse<PagedResponseDto<DepoModel>>>(`${this.apiBaseUrl}/api/muhasebe/depolar/paged`, { params }).pipe(map(this.unwrap<PagedResponseDto<DepoModel>>('Depolar alinamadi.')));
    }

    create(payload: CreateDepoRequest): Observable<DepoModel> {
        return this.http.post<ApiResponse<DepoModel>>(`${this.apiBaseUrl}/api/muhasebe/depolar`, payload).pipe(map(this.unwrap<DepoModel>('Depo olusturulamadi.')));
    }

    update(id: number, payload: UpdateDepoRequest): Observable<DepoModel> {
        return this.http.put<ApiResponse<DepoModel>>(`${this.apiBaseUrl}/api/muhasebe/depolar/${id}`, payload).pipe(map(this.unwrap<DepoModel>('Depo guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/depolar/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Depo silinemedi.');
        }));
    }

    getTesisler(): Observable<MuhasebeTesisModel[]> {
        return this.http.get<ApiResponse<MuhasebeTesisModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(map(this.unwrap<MuhasebeTesisModel[]>('Tesis listesi alinamadi.')));
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
