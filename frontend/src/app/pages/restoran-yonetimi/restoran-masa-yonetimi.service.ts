import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { CreateRestoranMasaRequest, RestoranMasaModel, UpdateRestoranMasaRequest } from './restoran-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class RestoranMasaYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<RestoranMasaModel[]> {
        return this.http.get<ApiResponse<RestoranMasaModel[]>>(`${this.apiBaseUrl}/api/restoran-masalari`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa listesi alinamadi.');
            })
        );
    }

    getByRestoranId(restoranId: number): Observable<RestoranMasaModel[]> {
        return this.http.get<ApiResponse<RestoranMasaModel[]>>(`${this.apiBaseUrl}/api/restoranlar/${restoranId}/masalar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restorana gore masa listesi alinamadi.');
            })
        );
    }

    getById(id: number): Observable<RestoranMasaModel> {
        return this.http.get<ApiResponse<RestoranMasaModel>>(`${this.apiBaseUrl}/api/restoran-masalari/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa detayi alinamadi.');
            })
        );
    }

    create(payload: CreateRestoranMasaRequest): Observable<RestoranMasaModel> {
        return this.http.post<ApiResponse<RestoranMasaModel>>(`${this.apiBaseUrl}/api/restoran-masalari`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateRestoranMasaRequest): Observable<RestoranMasaModel> {
        return this.http.put<ApiResponse<RestoranMasaModel>>(`${this.apiBaseUrl}/api/restoran-masalari/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/restoran-masalari/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa silinemedi.');
            })
        );
    }
}
