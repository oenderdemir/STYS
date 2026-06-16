import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { AssignUserKurumRequest, UpdateUserKurumRequest } from './user-kurum.request';
import { UserKurumModel } from './user-kurum.model';

@Injectable({ providedIn: 'root' })
export class KurumKullaniciService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getByUser(userId: string): Observable<UserKurumModel[]> {
        return this.http.get<ApiResponse<UserKurumModel[]>>(`${this.apiBaseUrl}/ui/kurum-kullanicilari/by-user/${userId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici kurum listesi alinamadi.');
            })
        );
    }

    getByKurum(kurumId: number): Observable<UserKurumModel[]> {
        return this.http.get<ApiResponse<UserKurumModel[]>>(`${this.apiBaseUrl}/ui/kurum-kullanicilari/by-kurum/${kurumId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum kullanici listesi alinamadi.');
            })
        );
    }

    assign(payload: AssignUserKurumRequest): Observable<UserKurumModel> {
        return this.http.post<ApiResponse<UserKurumModel>>(`${this.apiBaseUrl}/ui/kurum-kullanicilari/assign`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici kurum atamasi yapilamadi.');
            })
        );
    }

    update(id: string, payload: UpdateUserKurumRequest): Observable<UserKurumModel> {
        return this.http.put<ApiResponse<UserKurumModel>>(`${this.apiBaseUrl}/ui/kurum-kullanicilari/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici kurum atamasi guncellenemedi.');
            })
        );
    }

    delete(id: string): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kurum-kullanicilari/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici kurum atamasi silinemedi.');
            })
        );
    }
}
