import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    AddMasaOturumuKalemiRequest,
    CreateMasaOturumuRequest,
    GarsonMasaModel,
    GarsonMenuModel,
    MasaOturumuModel,
    UpdateMasaOturumuDurumRequest,
    UpdateMasaOturumuKalemiRequest,
    UpdateMasaOturumuNotRequest
} from './garson-servis.dto';

@Injectable({ providedIn: 'root' })
export class GarsonServisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getMasalar(restoranId: number): Observable<GarsonMasaModel[]> {
        return this.http.get<ApiResponse<GarsonMasaModel[]>>(`${this.apiBaseUrl}/api/garson/restoranlar/${restoranId}/masalar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa listesi alinamadi.');
            })
        );
    }

    getMasaOturumuByMasa(masaId: number): Observable<MasaOturumuModel> {
        return this.http.get<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/masalar/${masaId}/oturum`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa oturumu alinamadi.');
            })
        );
    }

    startOrGetMasaOturumu(masaId: number, payload: CreateMasaOturumuRequest): Observable<MasaOturumuModel> {
        return this.http.post<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/masalar/${masaId}/oturum`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Masa oturumu baslatilamadi.');
            })
        );
    }

    addKalem(oturumId: number, payload: AddMasaOturumuKalemiRequest): Observable<MasaOturumuModel> {
        return this.http.post<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/oturumlar/${oturumId}/kalemler`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kalem eklenemedi.');
            })
        );
    }

    updateKalem(oturumId: number, kalemId: number, payload: UpdateMasaOturumuKalemiRequest): Observable<MasaOturumuModel> {
        return this.http.put<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/oturumlar/${oturumId}/kalemler/${kalemId}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kalem guncellenemedi.');
            })
        );
    }

    deleteKalem(oturumId: number, kalemId: number): Observable<MasaOturumuModel> {
        return this.http.delete<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/oturumlar/${oturumId}/kalemler/${kalemId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kalem silinemedi.');
            })
        );
    }

    updateNot(oturumId: number, payload: UpdateMasaOturumuNotRequest): Observable<MasaOturumuModel> {
        return this.http.put<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/oturumlar/${oturumId}/not`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oturum notu guncellenemedi.');
            })
        );
    }

    updateDurum(oturumId: number, payload: UpdateMasaOturumuDurumRequest): Observable<MasaOturumuModel> {
        return this.http.put<ApiResponse<MasaOturumuModel>>(`${this.apiBaseUrl}/api/garson/oturumlar/${oturumId}/durum`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oturum durumu guncellenemedi.');
            })
        );
    }

    getMenu(restoranId: number): Observable<GarsonMenuModel> {
        return this.http.get<ApiResponse<GarsonMenuModel>>(`${this.apiBaseUrl}/api/garson/restoranlar/${restoranId}/menu`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Garson menusu alinamadi.');
            })
        );
    }
}
