import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    PosCihaziDto,
    PosCihaziKaydetRequest,
    PosOperationalReadinessDto,
    PosOdemeIslemiDto,
    PosOdemeSlipDto,
    PosPaymentBaslatRequestDto,
    PosSaglayiciDto,
    PosTerminalDto,
    PosTerminalKaydetRequest
} from './pos-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class PosYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(kurumId?: number | null, tesisId?: number | null): Observable<PosCihaziDto[]> {
        let params = new HttpParams();
        if (kurumId != null && kurumId > 0) {
            params = params.set('kurumId', kurumId);
        }
        if (tesisId != null && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<PosCihaziDto[]>>(`${this.apiBaseUrl}/ui/pos/cihazlar`, { params }).pipe(map(r => {
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

    getReadiness(cihazId: number): Observable<PosOperationalReadinessDto> {
        return this.http.get<ApiResponse<PosOperationalReadinessDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/readiness`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Hazırlık bilgisi alınamadı.');
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

    getReceipts(paymentId: number): Observable<PosOdemeSlipDto[]> {
        return this.http.get<ApiResponse<PosOdemeSlipDto[]>>(`${this.apiBaseUrl}/ui/pos/payments/${paymentId}/receipts`).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Slip listesi alınamadı.');
        }));
    }

    getReceiptContent(paymentId: number, receiptId: number): Observable<Blob> {
        return this.http.get(`${this.apiBaseUrl}/ui/pos/payments/${paymentId}/receipts/${receiptId}/content`, {
            responseType: 'blob'
        });
    }

    recoverReceipts(cihazId: number, posOdemeIslemiId: number): Observable<PosOdemeIslemiDto> {
        return this.http.post<ApiResponse<PosOdemeIslemiDto>>(`${this.apiBaseUrl}/ui/pos/cihazlar/${cihazId}/payment-test/${posOdemeIslemiId}/receipts/recover`, {}).pipe(map(r => {
            if (r.success && r.data) return r.data;
            throw new Error(tryReadApiMessage(r) ?? 'Slip kurtarma komutu gönderilemedi.');
        }));
    }
}
