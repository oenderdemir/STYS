import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { UserGroupResponseDto } from '../../core/identity';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { UserRequestDto, UserResetPasswordRequestDto, UserResponseDto } from './dto';

@Injectable({ providedIn: 'root' })
export class KullaniciYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getUsers(): Observable<UserResponseDto[]> {
        return this.http.get<ApiResponse<UserResponseDto[]>>(`${this.apiBaseUrl}/ui/user`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici listesi alinamadi.');
            })
        );
    }

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

    getTesisler(): Observable<TesisDto[]> {
        return this.http.get<ApiResponse<TesisDto[]>>(`${this.apiBaseUrl}/ui/tesis`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    createUser(payload: UserRequestDto): Observable<UserResponseDto> {
        return this.http.post<ApiResponse<UserResponseDto>>(`${this.apiBaseUrl}/ui/user`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici olusturulamadi.');
            })
        );
    }

    createResepsiyonistUserForTesis(tesisId: number, payload: UserRequestDto): Observable<UserResponseDto> {
        return this.http.post<ApiResponse<UserResponseDto>>(`${this.apiBaseUrl}/ui/tesis/${tesisId}/resepsiyonist-kullanici`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Resepsiyonist kullanici olusturulamadi.');
            })
        );
    }

    updateUser(id: string, payload: UserRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/user/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici guncellenemedi.');
            })
        );
    }

    resetUserPassword(id: string, payload: UserResetPasswordRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/user/${id}/password`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici parolasi degistirilemedi.');
            })
        );
    }

    deleteUser(id: string): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/user/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kullanici silinemedi.');
            })
        );
    }
}
