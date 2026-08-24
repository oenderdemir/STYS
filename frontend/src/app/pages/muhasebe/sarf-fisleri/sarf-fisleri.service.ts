import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { AddSarfFisiSatirRequest, CreateSarfFisiRequest, IptalSarfFisiRequest, SarfBirimSecenekModel, SarfFisiModel, UpdateSarfFisiSatirlarRequest } from './sarf-fisleri.dto';

@Injectable({ providedIn: 'root' })
export class SarfFisleriService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null, depoId?: number | null): Observable<PagedResponseDto<SarfFisiModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        if (depoId && depoId > 0) {
            params = params.set('depoId', depoId);
        }

        return this.http.get<ApiResponse<PagedResponseDto<SarfFisiModel>>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/paged`, { params })
            .pipe(map(this.unwrap<PagedResponseDto<SarfFisiModel>>('Sarf fişleri alınamadı.')));
    }

    getById(id: number): Observable<SarfFisiModel> {
        return this.http.get<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}`)
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi alınamadı.')));
    }

    getBirimler(tesisId: number): Observable<SarfBirimSecenekModel[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<SarfBirimSecenekModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/birimler`, { params })
            .pipe(map(this.unwrap<SarfBirimSecenekModel[]>('Birimler alınamadı.')));
    }

    create(payload: CreateSarfFisiRequest): Observable<SarfFisiModel> {
        return this.http.post<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri`, payload)
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi oluşturulamadı.')));
    }

    update(id: number, payload: CreateSarfFisiRequest): Observable<SarfFisiModel> {
        return this.http.put<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}`, payload)
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi güncellenemedi.')));
    }

    updateSatirlar(id: number, payload: UpdateSarfFisiSatirlarRequest): Observable<SarfFisiModel> {
        return this.http.put<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}/satirlar`, payload)
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi satırları kaydedilemedi.')));
    }

    addSatir(id: number, payload: AddSarfFisiSatirRequest): Observable<SarfFisiModel> {
        return this.http.post<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}/satirlar`, payload)
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi satırı eklenemedi.')));
    }

    deleteSatir(id: number, satirId: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}/satirlar/${satirId}`)
            .pipe(map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Sarf fişi satırı silinemedi.');
            }));
    }

    kesinlestir(id: number): Observable<SarfFisiModel> {
        return this.http.post<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}/kesinlestir`, {})
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi kesinleştirilemedi.')));
    }

    iptal(id: number, payload?: IptalSarfFisiRequest): Observable<SarfFisiModel> {
        return this.http.post<ApiResponse<SarfFisiModel>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-fisleri/${id}/iptal`, payload ?? {})
            .pipe(map(this.unwrap<SarfFisiModel>('Sarf fişi iptal edilemedi.')));
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
