import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    PosCihaziDto,
    PosCihaziKaydetRequest,
    PosOdemeIslemiDto,
    PosPaymentBaslatRequestDto,
    PosSaglayiciDto,
    PosTerminalDto,
    PosTerminalKaydetRequest
} from './pos-yonetimi.dto';

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

    startPairing(cihazId: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/pairing`, {}).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Eşleştirme başlatılamadı.');
        }));
    }

    ping(cihazId: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/ping`, {}).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Bağlantı testi başlatılamadı.');
        }));
    }

    getDeviceInfo(cihazId: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/device-info`, {}).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Cihaz bilgisi alınamadı.');
        }));
    }

    syncTerminals(cihazId: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/terminal-discovery`, {}).pipe(map(r => {
            if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Terminal senkronizasyonu başlatılamadı.');
        }));
    }

    getPaymentTests(cihazId: number, take = 5): Observable<PosOdemeIslemiDto[]> {
        return this.http.get<ApiResponse<PosOdemeIslemiDto[]>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/payment-test`, {
            params: { take }
        }).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Ödeme geçmişi alınamadı.');
        }));
    }

    startPaymentTest(cihazId: number, req: PosPaymentBaslatRequestDto): Observable<PosOdemeIslemiDto> {
        return this.http.post<ApiResponse<PosOdemeIslemiDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/payment-test`, req).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Ödeme başlatılamadı.');
        }));
    }

    getPaymentTestResult(cihazId: number, posOdemeIslemiId: number): Observable<PosOdemeIslemiDto> {
        return this.http.post<ApiResponse<PosOdemeIslemiDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/payment-test/${posOdemeIslemiId}/result`, {}).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Ödeme sonucu sorgulanamadı.');
        }));
    }
}
