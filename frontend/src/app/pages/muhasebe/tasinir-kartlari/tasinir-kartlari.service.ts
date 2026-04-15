import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateTasinirKartRequest, TasinirKartModel, UpdateTasinirKartRequest } from './tasinir-kartlari.dto';

@Injectable({ providedIn: 'root' })
export class TasinirKartlariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<TasinirKartModel[]> {
        return this.http.get<ApiResponse<TasinirKartModel[]>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari`).pipe(map(this.unwrap<TasinirKartModel[]>('Tasinir kartlar alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<TasinirKartModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<TasinirKartModel>>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari/paged`, { params }).pipe(map(this.unwrap<PagedResponseDto<TasinirKartModel>>('Tasinir kartlar alinamadi.')));
    }

    create(payload: CreateTasinirKartRequest): Observable<TasinirKartModel> {
        return this.http.post<ApiResponse<TasinirKartModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari`, payload).pipe(map(this.unwrap<TasinirKartModel>('Tasinir kart olusturulamadi.')));
    }

    update(id: number, payload: UpdateTasinirKartRequest): Observable<TasinirKartModel> {
        return this.http.put<ApiResponse<TasinirKartModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari/${id}`, payload).pipe(map(this.unwrap<TasinirKartModel>('Tasinir kart guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Tasinir kart silinemedi.');
        }));
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
