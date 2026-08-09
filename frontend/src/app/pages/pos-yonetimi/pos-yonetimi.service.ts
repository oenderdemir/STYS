import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { PosCihaziDto, PosCihaziKaydetRequest, PosSaglayiciDto, PosTerminalDto, PosTerminalKaydetRequest } from './pos-yonetimi.dto';

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

    getSaglayicilar(): Observable<PosSaglayiciDto[]> {
        return this.http.get<ApiResponse<PosSaglayiciDto[]>>(`${this.apiBaseUrl}/ui/pos/saglayicilar`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Sağlayıcı listesi alınamadı.');
        }));
    }

    getTerminals(cihazId: number): Observable<PosTerminalDto[]> {
        return this.http.get<ApiResponse<PosTerminalDto[]>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Terminal listesi alınamadı.');
        }));
    }

    createTerminal(cihazId: number, req: PosTerminalKaydetRequest): Observable<PosTerminalDto> {
        return this.http.post<ApiResponse<PosTerminalDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller`, req).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Terminal oluşturulamadı.');
        }));
    }

    updateTerminal(cihazId: number, id: number, req: PosTerminalKaydetRequest): Observable<PosTerminalDto> {
        return this.http.put<ApiResponse<PosTerminalDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller/${id}`, req).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Terminal güncellenemedi.');
        }));
    }

    deleteTerminal(cihazId: number, id: number): Observable<void> {
        return this.http.delete<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller/${id}`).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Terminal silinemedi.');
        }));
    }

    startTerminalPairing(cihazId: number, id: number): Observable<PosTerminalDto> {
        return this.http.post<ApiResponse<PosTerminalDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller/${id}/eslestir`, {}).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Terminal eşleştirme başlatılamadı.');
        }));
    }

    checkTerminalPairing(cihazId: number, id: number): Observable<PosTerminalDto> {
        return this.http.post<ApiResponse<PosTerminalDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminaller/${id}/eslestirme-kontrol`, {}).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Terminal eşleşme durumu alınamadı.');
        }));
    }
}
