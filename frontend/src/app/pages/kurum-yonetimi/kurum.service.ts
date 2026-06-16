import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { KurumModel } from './kurum.model';
import { CreateKurumRequest, UpdateKurumRequest } from './kurum.request';

@Injectable({ providedIn: 'root' })
export class KurumService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<KurumModel[]> {
        return this.http.get<ApiResponse<KurumModel[]>>(`${this.apiBaseUrl}/ui/kurum`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum listesi alinamadi.');
            })
        );
    }

    getById(id: number): Observable<KurumModel> {
        return this.http.get<ApiResponse<KurumModel>>(`${this.apiBaseUrl}/ui/kurum/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum detayi alinamadi.');
            })
        );
    }

    create(payload: CreateKurumRequest): Observable<KurumModel> {
        return this.http.post<ApiResponse<KurumModel>>(`${this.apiBaseUrl}/ui/kurum`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateKurumRequest): Observable<KurumModel> {
        return this.http.put<ApiResponse<KurumModel>>(`${this.apiBaseUrl}/ui/kurum/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kurum/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum silinemedi.');
            })
        );
    }
}
