import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { MenuItemDto } from '../../core/menu';
import { MenuItemRequestDto } from './dto';

@Injectable({ providedIn: 'root' })
export class MenuYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getMenuItems(): Observable<MenuItemDto[]> {
        return this.http.get<ApiResponse<MenuItemDto[]>>(`${this.apiBaseUrl}/ui/menuitem`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu listesi alinamadi.');
            })
        );
    }

    createMenuItem(payload: MenuItemRequestDto): Observable<MenuItemDto> {
        return this.http.post<ApiResponse<MenuItemDto>>(`${this.apiBaseUrl}/ui/menuitem`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu olusturulamadi.');
            })
        );
    }

    updateMenuItem(id: string, payload: MenuItemRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/menuitem/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu guncellenemedi.');
            })
        );
    }

    deleteMenuItem(id: string): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/menuitem/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Menu silinemedi.');
            })
        );
    }
}
