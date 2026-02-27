import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { UserGroupRequestDto, UserGroupResponseDto } from '../../core/identity';

@Injectable({ providedIn: 'root' })
export class KullaniciGrupYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getUserGroups(): Observable<UserGroupResponseDto[]> {
        return this.http.get<ApiResponse<UserGroupResponseDto[]>>(`${this.apiBaseUrl}/ui/usergroup`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici grup listesi alinamadi.');
            })
        );
    }

    createUserGroup(payload: UserGroupRequestDto): Observable<UserGroupResponseDto> {
        return this.http.post<ApiResponse<UserGroupResponseDto>>(`${this.apiBaseUrl}/ui/usergroup`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici grubu olusturulamadi.');
            })
        );
    }

    updateUserGroup(id: string, payload: UserGroupRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/usergroup/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici grubu guncellenemedi.');
            })
        );
    }

    deleteUserGroup(id: string): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/usergroup/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici grubu silinemedi.');
            })
        );
    }
}
