import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateMuhasebeHesapPlaniRequest,
    MuhasebeHesapPlaniModel,
    UpdateMuhasebeHesapPlaniRequest
} from './muhasebe-hesap-plani.dto';

@Injectable({ providedIn: 'root' })
export class MuhasebeHesapPlaniService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTree(): Observable<MuhasebeHesapPlaniModel[]> {
        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/tree`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alinamadi.');
            })
        );
    }

    getTreeRoots(): Observable<MuhasebeHesapPlaniModel[]> {
        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/tree/roots`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani kokleri alinamadi.');
            })
        );
    }

    getTreeChildren(parentId: number | null): Observable<MuhasebeHesapPlaniModel[]> {
        let params = new HttpParams();
        if (parentId !== null && Number.isFinite(parentId)) {
            params = params.set('parentId', parentId);
        }

        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/tree/children`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alt kayitlari alinamadi.');
            })
        );
    }

    getPaged(pageNumber: number, pageSize: number): Observable<PagedResponseDto<MuhasebeHesapPlaniModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<MuhasebeHesapPlaniModel>>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/paged`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alinamadi.');
            })
        );
    }

    create(payload: CreateMuhasebeHesapPlaniRequest): Observable<MuhasebeHesapPlaniModel> {
        return this.http.post<ApiResponse<MuhasebeHesapPlaniModel>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani`, payload).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateMuhasebeHesapPlaniRequest): Observable<MuhasebeHesapPlaniModel> {
        return this.http.put<ApiResponse<MuhasebeHesapPlaniModel>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/${id}`, payload).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/muhasebe/hesap-plani/${id}`).pipe(
            map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap silinemedi.');
            })
        );
    }
}
