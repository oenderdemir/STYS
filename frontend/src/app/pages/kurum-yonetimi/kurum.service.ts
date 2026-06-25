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

    getMyKurumlar(): Observable<KurumModel[]> {
        return this.http.get<ApiResponse<KurumModel[]>>(`${this.apiBaseUrl}/ui/kurum/benim-kurumlarim`).pipe(
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

    uploadLogo(kurumId: number, file: File): Observable<KurumModel> {
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post<ApiResponse<KurumModel>>(
            `${this.apiBaseUrl}/ui/kurum/${kurumId}/logo`,
            formData
        ).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Logo yuklenemedi.');
            })
        );
    }

    deleteLogo(kurumId: number): Observable<void> {
        return this.http.delete<void>(`${this.apiBaseUrl}/ui/kurum/${kurumId}/logo`).pipe(
            map(() => void 0)
        );
    }

    buildLogoUrl(logoUrl: string): string {
        const base = this.apiBaseUrl.replace(/\/$/, '');
        const normalizedPath = logoUrl.startsWith('/') ? logoUrl : `/${logoUrl}`;
        return `${base}${normalizedPath}`;
    }
}
