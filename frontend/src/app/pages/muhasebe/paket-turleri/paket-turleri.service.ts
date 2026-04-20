import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreatePaketTuruRequest, PaketTuruModel, UpdatePaketTuruRequest } from './paket-turleri.dto';

@Injectable({ providedIn: 'root' })
export class PaketTurleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<PaketTuruModel[]> {
        return this.http.get<ApiResponse<PaketTuruModel[]>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri`).pipe(map(this.unwrap<PaketTuruModel[]>('Paket turleri alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<PaketTuruModel>> {
        const params = new URLSearchParams({
            pageNumber: String(pageNumber),
            pageSize: String(pageSize)
        });

        return this.http.get<ApiResponse<PagedResponseDto<PaketTuruModel>>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri/paged?${params.toString()}`).pipe(map(this.unwrap<PagedResponseDto<PaketTuruModel>>('Paket turleri alinamadi.')));
    }

    create(payload: CreatePaketTuruRequest): Observable<PaketTuruModel> {
        return this.http.post<ApiResponse<PaketTuruModel>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri`, payload).pipe(map(this.unwrap<PaketTuruModel>('Paket turu olusturulamadi.')));
    }

    update(id: number, payload: UpdatePaketTuruRequest): Observable<PaketTuruModel> {
        return this.http.put<ApiResponse<PaketTuruModel>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri/${id}`, payload).pipe(map(this.unwrap<PaketTuruModel>('Paket turu guncellenemedi.')));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri/${id}`).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }

            throw new Error(tryReadApiMessage(envelope) ?? 'Paket turu silinemedi.');
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
