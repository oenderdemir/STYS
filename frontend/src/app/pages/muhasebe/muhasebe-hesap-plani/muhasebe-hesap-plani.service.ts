import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateMuhasebeHesapPlaniRequest,
    MuhasebeDetayHesapOlusturRequest,
    MuhasebeHesapPlaniModel,
    UpdateMuhasebeHesapPlaniRequest
} from './muhasebe-hesap-plani.dto';

@Injectable({ providedIn: 'root' })
export class MuhasebeHesapPlaniService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTree(tesisId?: number): Observable<MuhasebeHesapPlaniModel[]> {
        const params = tesisId && tesisId > 0 ? new HttpParams().set('tesisId', tesisId) : undefined;
        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/tree`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alinamadi.');
            })
        );
    }

    getTreeRoots(tesisId: number): Observable<MuhasebeHesapPlaniModel[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/tree/roots`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani kokleri alinamadi.');
            })
        );
    }

    getTreeChildren(parentId: number | null, tesisId: number): Observable<MuhasebeHesapPlaniModel[]> {
        let params = new HttpParams().set('tesisId', tesisId);
        if (parentId !== null && Number.isFinite(parentId)) {
            params = params.set('parentId', parentId);
        }

        return this.http.get<ApiResponse<MuhasebeHesapPlaniModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/tree/children`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alt kayitlari alinamadi.');
            })
        );
    }

    getPaged(pageNumber: number, pageSize: number, tesisId: number): Observable<PagedResponseDto<MuhasebeHesapPlaniModel>> {
        const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('tesisId', tesisId);
        return this.http.get<ApiResponse<PagedResponseDto<MuhasebeHesapPlaniModel>>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/paged`, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap plani alinamadi.');
            })
        );
    }

    create(payload: CreateMuhasebeHesapPlaniRequest): Observable<MuhasebeHesapPlaniModel> {
        return this.http.post<ApiResponse<MuhasebeHesapPlaniModel>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani`, payload).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap olusturulamadi.');
            })
        );
    }

    createDetayHesap(anaHesapId: number, ad: string, tesisId: number | null): Observable<MuhasebeHesapPlaniModel> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        const payload: MuhasebeDetayHesapOlusturRequest = { ad };
        return this.http.post<ApiResponse<MuhasebeHesapPlaniModel>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/${anaHesapId}/detay-hesap`, payload, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Detay hesap olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateMuhasebeHesapPlaniRequest, tesisId: number): Observable<MuhasebeHesapPlaniModel> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.put<ApiResponse<MuhasebeHesapPlaniModel>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/${id}`, payload, { params }).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap guncellenemedi.');
            })
        );
    }

    delete(id: number, tesisId: number): Observable<void> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/muhasebe/hesap-plani/${id}`, { params }).pipe(
            map((envelope) => {
                if (envelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Muhasebe hesap silinemedi.');
            })
        );
    }
}
