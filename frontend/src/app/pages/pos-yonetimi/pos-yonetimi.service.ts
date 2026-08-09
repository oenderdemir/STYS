import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { PosCihaziDto, PosCihaziKaydetRequest } from './pos-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class PosYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<PosCihaziDto[]> {
        return this.http.get<ApiResponse<PosCihaziDto[]>>(`${this.apiBaseUrl}/ui/pos/cihazlar`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Liste alınamadı.');
        }));
    }

    getById(id: number): Observable<PosCihaziDto> {
        return this.http.get<ApiResponse<PosCihaziDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${id}`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Cihaz bilgisi alınamadı.');
        }));
    }

    create(req: PosCihaziKaydetRequest): Observable<PosCihaziDto> {
        return this.http.post<ApiResponse<PosCihaziDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar`, req).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Cihaz oluşturulamadı.');
        }));
    }

    update(id: number, req: PosCihaziKaydetRequest): Observable<PosCihaziDto> {
        return this.http.put<ApiResponse<PosCihaziDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${id}`, req).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Cihaz güncellenemedi.');
        }));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${id}`).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Cihaz silinemedi.');
        }));
    }
}
