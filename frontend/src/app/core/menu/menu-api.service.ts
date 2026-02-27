import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse } from '../api';
import { getApiBaseUrl } from '../config';
import { MenuItemDto } from './dto';

@Injectable({ providedIn: 'root' })
export class MenuApiService {
    private readonly http = inject(HttpClient);
    private readonly menuTreeUrl = `${getApiBaseUrl()}/ui/menuitem/tree`;

    getMenuTree(): Observable<MenuItemDto[]> {
        return this.http.get<ApiResponse<MenuItemDto[]>>(this.menuTreeUrl).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                return [];
            })
        );
    }
}
