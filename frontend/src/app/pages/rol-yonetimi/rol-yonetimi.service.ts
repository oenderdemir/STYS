import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { RoleResponseDto } from '../../core/identity';
import { RoleRequestDto } from './dto';

@Injectable({ providedIn: 'root' })
export class RolYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getRoles(): Observable<RoleResponseDto[]> {
        return this.http.get<ApiResponse<RoleResponseDto[]>>(`${this.apiBaseUrl}/ui/role`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rol listesi alinamadi.');
            })
        );
    }

    getViewRoles(): Observable<RoleResponseDto[]> {
        return this.http.get<ApiResponse<RoleResponseDto[]>>(`${this.apiBaseUrl}/ui/role/view-roles`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'View rolleri alinamadi.');
            })
        );
    }

    createRole(payload: RoleRequestDto): Observable<RoleResponseDto> {
        return this.http.post<ApiResponse<RoleResponseDto>>(`${this.apiBaseUrl}/ui/role`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rol olusturulamadi.');
            })
        );
    }

    updateRole(id: string, payload: RoleRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/role/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rol guncellenemedi.');
            })
        );
    }

    deleteRole(id: string): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/role/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rol silinemedi.');
            })
        );
    }
}
