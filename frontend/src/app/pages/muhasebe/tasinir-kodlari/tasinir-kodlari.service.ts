import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateTasinirKodRequest, ImportTasinirKodlariRequest, TasinirKodImportSonucModel, TasinirKodModel, UpdateTasinirKodRequest } from './tasinir-kodlari.dto';

@Injectable({ providedIn: 'root' })
export class TasinirKodlariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<TasinirKodModel[]> {
        return this.http.get<ApiResponse<TasinirKodModel[]>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari`).pipe(map(this.unwrapList('Tasinir kodlar alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<TasinirKodModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<TasinirKodModel>>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/paged`, { params }).pipe(map(this.unwrapOne('Tasinir kodlar alinamadi.')));
    }

    searchPaged(pageNumber: number, pageSize: number, query: string): Observable<PagedResponseDto<TasinirKodModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<TasinirKodModel>>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/paged`, { params }).pipe(map(this.unwrapOne('Tasinir kodlar alinamadi.')));
    }

    getById(id: number): Observable<TasinirKodModel> {
        return this.http.get<ApiResponse<TasinirKodModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/${id}`).pipe(map(this.unwrapOne('Tasinir kod detayi alinamadi.')));
    }

    create(payload: CreateTasinirKodRequest): Observable<TasinirKodModel> {
        return this.http.post<ApiResponse<TasinirKodModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari`, payload).pipe(map(this.unwrapOne('Tasinir kod olusturulamadi.')));
    }

    update(id: number, payload: UpdateTasinirKodRequest): Observable<TasinirKodModel> {
        return this.http.put<ApiResponse<TasinirKodModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/${id}`, payload).pipe(map(this.unwrapOne('Tasinir kod guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Tasinir kod silinemedi.');
        }));
    }

    import(payload: ImportTasinirKodlariRequest): Observable<TasinirKodImportSonucModel> {
        return this.http.post<ApiResponse<TasinirKodImportSonucModel>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kodlari/import`, payload).pipe(map(this.unwrapOne('Import islemi basarisiz.')));
    }

    private unwrapList<T>(fallback: string) {
        return (envelope: ApiResponse<T>): T => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }
            throw new Error(tryReadApiMessage(envelope) ?? fallback);
        };
    }

    private unwrapOne<T>(fallback: string) {
        return (envelope: ApiResponse<T>): T => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }
            throw new Error(tryReadApiMessage(envelope) ?? fallback);
        };
    }
}
