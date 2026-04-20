import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateTasinirKartRequest, MuhasebeTesisModel, PaketTuruOptionModel, TasinirKartModel, UpdateTasinirKartRequest } from './tasinir-kartlari.dto';

@Injectable({ providedIn: 'root' })
export class TasinirKartlariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<TasinirKartModel[]> {
        return this.http.get<ApiResponse<TasinirKartModel[]>>(`${this.apiBaseUrl}/api/muhasebe/tasinir-kartlari`).pipe(map(this.unwrap<TasinirKartModel[]>('Tasinir kartlar alinamadi.')));
    }

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null): Observable<PagedResponseDto<TasinirKartModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
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

    getTesisler(): Observable<MuhasebeTesisModel[]> {
        return this.http.get<ApiResponse<MuhasebeTesisModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(map(this.unwrap<MuhasebeTesisModel[]>('Tesis listesi alinamadi.')));
    }

    getPaketTurleri(): Observable<PaketTuruOptionModel[]> {
        return this.http.get<ApiResponse<PaketTuruOptionModel[]>>(`${this.apiBaseUrl}/api/muhasebe/paket-turleri`).pipe(map(this.unwrap<PaketTuruOptionModel[]>('Paket turleri alinamadi.')));
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
